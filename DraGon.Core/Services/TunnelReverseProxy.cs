using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace DraGon.Core.Services;

/// <summary>
/// Lightweight local HTTP/1.1 reverse proxy that sits between cloudflared and the local Nginx/Apache server.
/// Preserves the public *.trycloudflare.com host/protocol via X-Forwarded-* headers while routing to the
/// project's local virtual host, and rewrites local-domain redirects, cookies, and text links back to the
/// public tunnel URL so browsers do not bounce back to the local domain.
/// </summary>
public sealed partial class TunnelReverseProxy : IDisposable
{
    private const int MaxRewriteBodyBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailers",
        "Transfer-Encoding",
        "Upgrade"
    };

    private static readonly string[] RewritableContentTypes =
    [
        "text/html",
        "text/css",
        "application/javascript",
        "text/javascript",
        "application/x-javascript",
        "application/json",
        "application/xml",
        "text/xml"
    ];

    private readonly string _localDomain;
    private readonly int _localPort;
    private readonly AppLogger? _logger;
    private readonly TcpListener _listener;
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoopTask;
    private volatile string? _publicBaseUrl;
    private bool _disposed;

    public int ListenPort { get; }
    public string? PublicBaseUrl => _publicBaseUrl;

    public TunnelReverseProxy(string localDomain, int localPort, AppLogger? logger = null)
    {
        _localDomain = string.IsNullOrWhiteSpace(localDomain) ? "localhost" : localDomain.Trim();
        _localPort = localPort > 0 ? localPort : 80;
        _logger = logger;

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseProxy = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        ListenPort = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void SetPublicBaseUrl(string? publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            _publicBaseUrl = null;
            return;
        }

        _publicBaseUrl = publicBaseUrl.Trim().TrimEnd('/');
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch (OperationCanceledException)
            {
                client?.Dispose();
                break;
            }
            catch (ObjectDisposedException)
            {
                client?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                client?.Dispose();
                if (!ct.IsCancellationRequested)
                {
                    _logger?.Warn("TunnelProxy", $"Error accepting connection: {ex.Message}");
                }
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            client.NoDelay = true;
            await using var stream = client.GetStream();

            while (!ct.IsCancellationRequested)
            {
                var parsed = await ReadHttpRequestAsync(stream, ct);
                if (parsed is null)
                {
                    break;
                }

                var (method, rawTarget, httpVersion, headers, bodyBytes) = parsed.Value;
                var shouldClose = ShouldCloseConnection(httpVersion, headers);

                try
                {
                    var effectivePublicBase = ResolveEffectivePublicBaseUrl(headers);
                    var publicUri = Uri.TryCreate(effectivePublicBase, UriKind.Absolute, out var parsedPublicUri)
                        ? parsedPublicUri
                        : null;

                    var targetPath = rawTarget.StartsWith('/') ? rawTarget : "/" + rawTarget;
                    var upstreamUri = new Uri($"http://127.0.0.1:{_localPort}{targetPath}");

                    using var upstreamRequest = new HttpRequestMessage(new HttpMethod(method), upstreamUri);
                    upstreamRequest.Headers.Host = _localDomain;

                    if (bodyBytes is { Length: > 0 })
                    {
                        upstreamRequest.Content = new ByteArrayContent(bodyBytes);
                    }

                    foreach (var (name, value) in headers)
                    {
                        if (HopByHopHeaders.Contains(name) ||
                            name.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!upstreamRequest.Headers.TryAddWithoutValidation(name, value))
                        {
                            upstreamRequest.Content?.Headers.TryAddWithoutValidation(name, value);
                        }
                    }

                    // Ask loopback server for uncompressed payload so text URLs can be rewritten reliably.
                    // Cloudflare Edge automatically compresses responses for end users.
                    upstreamRequest.Headers.TryAddWithoutValidation("Accept-Encoding", "identity");

                    if (publicUri is not null)
                    {
                        upstreamRequest.Headers.Remove("X-Forwarded-Host");
                        upstreamRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", publicUri.Authority);

                        upstreamRequest.Headers.Remove("X-Forwarded-Proto");
                        upstreamRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", publicUri.Scheme);

                        upstreamRequest.Headers.Remove("X-Forwarded-Port");
                        var publicPort = publicUri.IsDefaultPort
                            ? (publicUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
                            : publicUri.Port;
                        upstreamRequest.Headers.TryAddWithoutValidation("X-Forwarded-Port", publicPort.ToString(CultureInfo.InvariantCulture));
                    }

                    using var upstreamResponse = await _httpClient.SendAsync(
                        upstreamRequest,
                        HttpCompletionOption.ResponseHeadersRead,
                        ct);

                    await WriteUpstreamResponseAsync(
                        stream,
                        method,
                        upstreamResponse,
                        effectivePublicBase,
                        shouldClose,
                        ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.Warn("TunnelProxy", $"Upstream error for {_localDomain} ({method} {rawTarget}): {ex.Message}");
                    await WriteSimpleErrorAsync(stream, 502, "Bad Gateway", $"DraGon Tunnel Proxy error: {ex.Message}", ct);
                    break;
                }

                if (shouldClose)
                {
                    break;
                }
            }
        }
    }

    private string? ResolveEffectivePublicBaseUrl(List<(string Name, string Value)> headers)
    {
        if (!string.IsNullOrWhiteSpace(_publicBaseUrl))
        {
            return _publicBaseUrl;
        }

        var hostHeader = headers.FirstOrDefault(h => h.Name.Equals("Host", StringComparison.OrdinalIgnoreCase)).Value;
        if (string.IsNullOrWhiteSpace(hostHeader))
        {
            return null;
        }

        var protoHeader = headers.FirstOrDefault(h => h.Name.Equals("X-Forwarded-Proto", StringComparison.OrdinalIgnoreCase)).Value;
        var scheme = string.Equals(protoHeader, "http", StringComparison.OrdinalIgnoreCase) ? "http" : "https";
        return $"{scheme}://{hostHeader.Trim()}";
    }

    private async Task WriteUpstreamResponseAsync(
        NetworkStream clientStream,
        string requestMethod,
        HttpResponseMessage response,
        string? publicBaseUrl,
        bool closeConnection,
        CancellationToken ct)
    {
        var statusCode = (int)response.StatusCode;
        var reasonPhrase = string.IsNullOrWhiteSpace(response.ReasonPhrase)
            ? response.StatusCode.ToString()
            : response.ReasonPhrase;

        var noBody = requestMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase) ||
                     statusCode is >= 100 and < 200 or 204 or 304;

        var contentType = response.Content.Headers.ContentType?.MediaType;
        var contentEncoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
        var contentLength = response.Content.Headers.ContentLength;

        var canRewriteBody = !noBody &&
                             !string.IsNullOrWhiteSpace(publicBaseUrl) &&
                             string.IsNullOrWhiteSpace(contentEncoding) &&
                             IsRewritableContentType(contentType) &&
                             (!contentLength.HasValue || contentLength.Value <= MaxRewriteBodyBytes);

        byte[]? bufferedBody = null;
        if (canRewriteBody)
        {
            var rawBytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (rawBytes.Length <= MaxRewriteBodyBytes)
            {
                var charset = response.Content.Headers.ContentType?.CharSet;
                var encoding = ResolveEncoding(charset);
                var originalText = encoding.GetString(rawBytes);
                var rewrittenText = RewriteResponseBody(originalText, _localDomain, _localPort, publicBaseUrl!);
                bufferedBody = ReferenceEquals(rewrittenText, originalText)
                    ? rawBytes
                    : encoding.GetBytes(rewrittenText);
            }
            else
            {
                bufferedBody = rawBytes;
            }
        }

        var headerBuilder = new StringBuilder();
        headerBuilder.Append($"HTTP/1.1 {statusCode} {reasonPhrase}\r\n");

        foreach (var header in response.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key))
            {
                continue;
            }

            foreach (var rawVal in header.Value)
            {
                var val = rawVal;
                if (header.Key.Equals("Location", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(publicBaseUrl))
                {
                    val = RewriteLocationHeader(rawVal, _localDomain, _localPort, publicBaseUrl);
                }
                else if (header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
                {
                    val = RewriteSetCookieHeader(rawVal, _localDomain);
                }

                headerBuilder.Append($"{header.Key}: {val}\r\n");
            }
        }

        foreach (var header in response.Content.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key) ||
                header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var rawVal in header.Value)
            {
                headerBuilder.Append($"{header.Key}: {rawVal}\r\n");
            }
        }

        var useChunked = false;
        if (noBody)
        {
            if (contentLength.HasValue)
            {
                headerBuilder.Append($"Content-Length: {contentLength.Value}\r\n");
            }
        }
        else if (bufferedBody is not null)
        {
            headerBuilder.Append($"Content-Length: {bufferedBody.Length}\r\n");
        }
        else if (contentLength.HasValue)
        {
            headerBuilder.Append($"Content-Length: {contentLength.Value}\r\n");
        }
        else
        {
            useChunked = true;
            headerBuilder.Append("Transfer-Encoding: chunked\r\n");
        }

        headerBuilder.Append(closeConnection ? "Connection: close\r\n\r\n" : "Connection: keep-alive\r\n\r\n");

        var headerBytes = Encoding.ASCII.GetBytes(headerBuilder.ToString());
        await clientStream.WriteAsync(headerBytes, ct);

        if (!noBody)
        {
            if (bufferedBody is not null)
            {
                if (bufferedBody.Length > 0)
                {
                    await clientStream.WriteAsync(bufferedBody, ct);
                }
            }
            else if (useChunked)
            {
                await using var upstreamStream = await response.Content.ReadAsStreamAsync(ct);
                var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
                try
                {
                    int read;
                    while ((read = await upstreamStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                    {
                        var chunkPrefix = Encoding.ASCII.GetBytes($"{read:X}\r\n");
                        await clientStream.WriteAsync(chunkPrefix, ct);
                        await clientStream.WriteAsync(buffer.AsMemory(0, read), ct);
                        await clientStream.WriteAsync("\r\n"u8.ToArray(), ct);
                    }

                    await clientStream.WriteAsync("0\r\n\r\n"u8.ToArray(), ct);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            else
            {
                await using var upstreamStream = await response.Content.ReadAsStreamAsync(ct);
                await upstreamStream.CopyToAsync(clientStream, ct);
            }
        }

        await clientStream.FlushAsync(ct);
    }

    private static async Task<(string Method, string Target, string Version, List<(string Name, string Value)> Headers, byte[]? Body)?> ReadHttpRequestAsync(
        NetworkStream stream,
        CancellationToken ct)
    {
        var headerBuffer = new MemoryStream();
        var singleByte = new byte[1];
        var matched = 0;

        while (true)
        {
            var read = await stream.ReadAsync(singleByte.AsMemory(0, 1), ct);
            if (read == 0)
            {
                return null;
            }

            var b = singleByte[0];
            headerBuffer.WriteByte(b);

            if (headerBuffer.Length > 64 * 1024)
            {
                return null;
            }

            matched = (matched, b) switch
            {
                (0, (byte)'\r') => 1,
                (1, (byte)'\n') => 2,
                (2, (byte)'\r') => 3,
                (3, (byte)'\n') => 4,
                (_, (byte)'\r') => 1,
                _ => 0
            };

            if (matched == 4)
            {
                break;
            }
        }

        var headerText = Encoding.Latin1.GetString(headerBuffer.GetBuffer(), 0, (int)headerBuffer.Length - 4);
        var lines = headerText.Split("\r\n");
        if (lines.Length == 0)
        {
            return null;
        }

        var requestLineParts = lines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (requestLineParts.Length < 3)
        {
            return null;
        }

        var method = requestLineParts[0];
        var target = requestLineParts[1];
        var version = requestLineParts[2];

        var headers = new List<(string Name, string Value)>(lines.Length);
        long contentLength = 0;
        var isChunked = false;

        for (var i = 1; i < lines.Length; i++)
        {
            var colonIdx = lines[i].IndexOf(':');
            if (colonIdx <= 0)
            {
                continue;
            }

            var name = lines[i][..colonIdx].Trim();
            var value = lines[i][(colonIdx + 1)..].Trim();
            headers.Add((name, value));

            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) &&
                long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLen) &&
                parsedLen > 0)
            {
                contentLength = parsedLen;
            }
            else if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) &&
                     value.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                isChunked = true;
            }
        }

        byte[]? body = null;
        if (isChunked)
        {
            body = await ReadChunkedBodyAsync(stream, ct);
        }
        else if (contentLength > 0)
        {
            body = new byte[contentLength];
            var totalRead = 0;
            while (totalRead < contentLength)
            {
                var read = await stream.ReadAsync(body.AsMemory(totalRead, (int)(contentLength - totalRead)), ct);
                if (read == 0)
                {
                    break;
                }
                totalRead += read;
            }

            if (totalRead < contentLength)
            {
                Array.Resize(ref body, totalRead);
            }
        }

        return (method, target, version, headers, body);
    }

    private static async Task<byte[]> ReadChunkedBodyAsync(NetworkStream stream, CancellationToken ct)
    {
        using var output = new MemoryStream();
        while (true)
        {
            var sizeLine = await ReadLineAsync(stream, ct);
            if (sizeLine is null)
            {
                break;
            }

            var semi = sizeLine.IndexOf(';');
            var hex = (semi >= 0 ? sizeLine[..semi] : sizeLine).Trim();
            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var chunkSize))
            {
                break;
            }

            if (chunkSize == 0)
            {
                // Consume trailing headers until empty line
                while (true)
                {
                    var trailer = await ReadLineAsync(stream, ct);
                    if (string.IsNullOrEmpty(trailer))
                    {
                        break;
                    }
                }
                break;
            }

            var chunk = new byte[chunkSize];
            var total = 0;
            while (total < chunkSize)
            {
                var read = await stream.ReadAsync(chunk.AsMemory(total, chunkSize - total), ct);
                if (read == 0)
                {
                    break;
                }
                total += read;
            }

            output.Write(chunk, 0, total);
            // Consume CRLF after chunk
            _ = await ReadLineAsync(stream, ct);
        }

        return output.ToArray();
    }

    private static async Task<string?> ReadLineAsync(NetworkStream stream, CancellationToken ct)
    {
        var bytes = new List<byte>(64);
        var buf = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buf.AsMemory(0, 1), ct);
            if (read == 0)
            {
                return bytes.Count == 0 ? null : Encoding.Latin1.GetString([.. bytes]);
            }

            if (buf[0] == (byte)'\n')
            {
                if (bytes.Count > 0 && bytes[^1] == (byte)'\r')
                {
                    bytes.RemoveAt(bytes.Count - 1);
                }
                return Encoding.Latin1.GetString([.. bytes]);
            }

            bytes.Add(buf[0]);
        }
    }

    private static bool ShouldCloseConnection(string httpVersion, List<(string Name, string Value)> headers)
    {
        var conn = headers.FirstOrDefault(h => h.Name.Equals("Connection", StringComparison.OrdinalIgnoreCase)).Value;
        if (!string.IsNullOrWhiteSpace(conn))
        {
            if (conn.Contains("close", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (conn.Contains("keep-alive", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return !httpVersion.Equals("HTTP/1.1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRewritableContentType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        foreach (var candidate in RewritableContentTypes)
        {
            if (mediaType.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim().Trim('"'));
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private static async Task WriteSimpleErrorAsync(
        NetworkStream stream,
        int statusCode,
        string reason,
        string message,
        CancellationToken ct)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(message);
        var header = $"HTTP/1.1 {statusCode} {reason}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), ct);
        await stream.WriteAsync(bodyBytes, ct);
        await stream.FlushAsync(ct);
    }

    public static string RewriteLocationHeader(
        string location,
        string localDomain,
        int localPort,
        string publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(location) ||
            string.IsNullOrWhiteSpace(localDomain) ||
            string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return location;
        }

        var normalizedPublic = publicBaseUrl.Trim().TrimEnd('/');
        var escapedDomain = Regex.Escape(localDomain.Trim());

        // Match http(s)://localDomain(:port)? or //localDomain(:port)?
        var domainPattern = $@"^(?:https?:)?//{escapedDomain}(?::\d+)?(?=$|/|\?|#)";
        var rewritten = Regex.Replace(location, domainPattern, normalizedPublic, RegexOptions.IgnoreCase);
        if (!ReferenceEquals(rewritten, location) && !string.Equals(rewritten, location, StringComparison.Ordinal))
        {
            return rewritten;
        }

        // Also handle redirects to 127.0.0.1(:localPort) or localhost(:localPort)
        var portPart = localPort is 80 or 443 ? $@"(?::{localPort}|:80|:443)?" : $@"(?::{localPort})";
        var loopbackPattern = $@"^https?://(?:127\.0\.0\.1|localhost){portPart}(?=$|/|\?|#)";
        return Regex.Replace(location, loopbackPattern, normalizedPublic, RegexOptions.IgnoreCase);
    }

    public static string RewriteSetCookieHeader(string setCookie, string localDomain)
    {
        if (string.IsNullOrWhiteSpace(setCookie) || string.IsNullOrWhiteSpace(localDomain))
        {
            return setCookie;
        }

        var escapedDomain = Regex.Escape(localDomain.Trim().TrimStart('.'));
        return Regex.Replace(
            setCookie,
            $@";\s*Domain=\.?{escapedDomain}(?=\s*;|\s*$)",
            string.Empty,
            RegexOptions.IgnoreCase);
    }

    public static string RewriteResponseBody(
        string body,
        string localDomain,
        int localPort,
        string publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(body) ||
            string.IsNullOrWhiteSpace(localDomain) ||
            string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return body;
        }

        var trimmedDomain = localDomain.Trim();
        if (!body.Contains(trimmedDomain, StringComparison.OrdinalIgnoreCase))
        {
            return body;
        }

        var normalizedPublic = publicBaseUrl.Trim().TrimEnd('/');
        var escapedDomain = Regex.Escape(trimmedDomain);

        // 1. Standard URLs: http://domain(:port)? or https://domain(:port)?
        var result = Regex.Replace(
            body,
            $@"https?://{escapedDomain}(?::\d+)?(?=[/'""\s?#<&]|$)",
            normalizedPublic,
            RegexOptions.IgnoreCase);

        // 2. JSON-escaped URLs: http:\/\/domain(:port)? or https:\/\/domain(:port)?
        var jsonPublic = normalizedPublic.Replace("/", @"\/");
        result = Regex.Replace(
            result,
            $@"https?:\\/\\/{escapedDomain}(?::\d+)?(?=\\/|['""\s?#<&]|$)",
            jsonPublic,
            RegexOptions.IgnoreCase);

        // 3. Protocol-relative URLs: //domain(:port)?
        if (Uri.TryCreate(normalizedPublic, UriKind.Absolute, out var publicUri))
        {
            result = Regex.Replace(
                result,
                $@"(?<=[""'(=,\s])//{escapedDomain}(?::\d+)?(?=[/'""\s?#<&]|$)",
                $"//{publicUri.Authority}",
                RegexOptions.IgnoreCase);
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // Ignore
        }

        try
        {
            _listener.Stop();
        }
        catch
        {
            // Ignore
        }

        _httpClient.Dispose();
        _cts.Dispose();
    }
}
