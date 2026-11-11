using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public sealed record SslCertificatePaths(string CertificatePemPath, string PrivateKeyPemPath);

public sealed class LocalSslService
{
    private const string RootCaCommonName = "DraGon Local Development Root CA";
    private readonly AppLogger _logger;
    private readonly string _certsDir;

    public LocalSslService(AppLogger logger, string? customCertsDirectory = null)
    {
        _logger = logger;
        _certsDir = string.IsNullOrWhiteSpace(customCertsDirectory)
            ? DraGonPaths.CertificatesDirectory
            : customCertsDirectory;
    }

    public string RootCaPfxFilePath => Path.Combine(_certsDir, "DraGon-Local-RootCA.pfx");
    public string RootCaCertPemFilePath => Path.Combine(_certsDir, "DraGon-Local-RootCA.crt");

    public SslCertificatePaths GetDomainCertificatePaths(string domain)
    {
        var normalized = domain.Trim().ToLowerInvariant();
        return new SslCertificatePaths(
            Path.Combine(_certsDir, $"{normalized}.crt"),
            Path.Combine(_certsDir, $"{normalized}.key"));
    }

    public bool HasDomainCertificate(string domain)
    {
        var check = ValidationService.ValidateDomain(domain);
        if (!check.IsValid)
        {
            return false;
        }

        var paths = GetDomainCertificatePaths(domain);
        return File.Exists(paths.CertificatePemPath) && File.Exists(paths.PrivateKeyPemPath);
    }

    public X509Certificate2 EnsureRootCaCertificate()
    {
        Directory.CreateDirectory(_certsDir);

        if (File.Exists(RootCaPfxFilePath))
        {
            try
            {
                var loaded = X509CertificateLoader.LoadPkcs12FromFile(
                    RootCaPfxFilePath,
                    password: null,
                    keyStorageFlags: X509KeyStorageFlags.Exportable);

                if (loaded.HasPrivateKey && loaded.NotAfter > DateTime.UtcNow.AddDays(30))
                {
                    if (!File.Exists(RootCaCertPemFilePath))
                    {
                        File.WriteAllText(RootCaCertPemFilePath, loaded.ExportCertificatePem(), new UTF8Encoding(false));
                    }

                    return loaded;
                }

                loaded.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Warn("SSL", $"Root CA lama tidak dapat dimuat ({ex.Message}), membuat ulang Root CA baru.");
            }
        }

        using var rsa = RSA.Create(2048);
        var subject = new X500DistinguishedName($"CN={RootCaCommonName}, O=DraGon Local Dev, C=ID");
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: true,
                hasPathLengthConstraint: true,
                pathLengthConstraint: 1,
                critical: true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature,
                critical: true));

        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddYears(10);

        using var createdCa = request.CreateSelfSigned(notBefore, notAfter);
        var pfxBytes = createdCa.Export(X509ContentType.Pfx);
        File.WriteAllBytes(RootCaPfxFilePath, pfxBytes);
        File.WriteAllText(RootCaCertPemFilePath, createdCa.ExportCertificatePem(), new UTF8Encoding(false));

        _logger.Info("SSL", $"Sertifikat Root CA Lokal DraGon berhasil dibuat di: {RootCaCertPemFilePath}");

        return X509CertificateLoader.LoadPkcs12(
            pfxBytes,
            password: null,
            keyStorageFlags: X509KeyStorageFlags.Exportable);
    }

    public SslCertificatePaths EnsureDomainCertificate(string domain)
    {
        var validation = ValidationService.ValidateDomain(domain);
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.ErrorMessage, nameof(domain));
        }

        var normalized = domain.Trim().ToLowerInvariant();
        var paths = GetDomainCertificatePaths(normalized);

        if (File.Exists(paths.CertificatePemPath) && File.Exists(paths.PrivateKeyPemPath))
        {
            return paths;
        }

        using var rootCa = EnsureRootCaCertificate();
        using var leafRsa = RSA.Create(2048);

        var subject = new X500DistinguishedName($"CN={normalized}, O=DraGon Local Dev, C=ID");
        var request = new CertificateRequest(subject, leafRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, critical: true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")], // Server Authentication
                critical: false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(normalized);
        sanBuilder.AddDnsName($"*.{normalized}");
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        var serialNumber = RandomNumberGenerator.GetBytes(16);
        serialNumber[0] &= 0x7F; // Ensure positive ASN.1 integer

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddDays(825); // Max leaf validity accepted by modern browsers

        using var signedLeaf = request.Create(rootCa, notBefore, notAfter, serialNumber);

        var certChainPem = new StringBuilder();
        certChainPem.AppendLine(signedLeaf.ExportCertificatePem());
        certChainPem.AppendLine(rootCa.ExportCertificatePem());

        var privateKeyPem = leafRsa.ExportPkcs8PrivateKeyPem();

        File.WriteAllText(paths.CertificatePemPath, certChainPem.ToString(), new UTF8Encoding(false));
        File.WriteAllText(paths.PrivateKeyPemPath, privateKeyPem, new UTF8Encoding(false));

        _logger.Info("SSL", $"Sertifikat HTTPS lokal untuk '{normalized}' berhasil diterbitkan.");
        return paths;
    }

    public void EnsureCertificatesForProjects(IEnumerable<ProjectItem> projects)
    {
        foreach (var project in projects)
        {
            if (!ValidationService.ValidateDomain(project.Domain).IsValid)
            {
                project.HasSslCertificate = false;
                continue;
            }

            if (project.HttpsEnabled)
            {
                try
                {
                    EnsureDomainCertificate(project.Domain);
                    project.HasSslCertificate = true;
                }
                catch (Exception ex)
                {
                    project.HasSslCertificate = false;
                    _logger.Error("SSL", $"Gagal membuat sertifikat SSL untuk {project.Domain}.", ex);
                }
            }
            else
            {
                project.HasSslCertificate = HasDomainCertificate(project.Domain);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    public bool IsRootCaTrustedInCurrentUserStore()
    {
        try
        {
            if (!File.Exists(RootCaPfxFilePath))
            {
                return false;
            }

            using var rootCa = EnsureRootCaCertificate();
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            var matches = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                rootCa.Thumbprint,
                validOnly: false);

            return matches.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public (bool Success, string Message) TrustRootCaInCurrentUserStore()
    {
        try
        {
            using var rootCa = EnsureRootCaCertificate();
            using var publicOnlyCert = X509CertificateLoader.LoadCertificate(rootCa.RawData);
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);

            var existing = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                publicOnlyCert.Thumbprint,
                validOnly: false);

            if (existing.Count == 0)
            {
                store.Add(publicOnlyCert);
            }

            _logger.Info("SSL", $"Root CA Lokal DraGon (Thumbprint: {publicOnlyCert.Thumbprint}) berhasil didaftarkan ke CurrentUser\\Root.");
            return (true, "Root CA Lokal DraGon berhasil dipercaya di penyimpanan sertifikat Windows (CurrentUser\\Root). Browser lokal kini mempercayai domain https://*.dra.");
        }
        catch (Exception ex)
        {
            _logger.Error("SSL", "Gagal memasang Root CA Lokal ke penyimpanan sertifikat Windows.", ex);
            return (false, $"Gagal memasang Root CA Lokal: {ex.Message}");
        }
    }
}
