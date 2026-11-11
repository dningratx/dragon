using System.Text;

namespace DraGon.Core.Services;

public sealed record LogEntry(DateTime Timestamp, string Level, string Source, string Message)
{
    public string Formatted => $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] [{Source}] {Message}";
}

public sealed class AppLogger
{
    private readonly Lock _fileLock = new();
    public event Action<LogEntry>? EntryWritten;

    public void Info(string source, string message) => Write("INFO", source, message);
    public void Warn(string source, string message) => Write("WARN", source, message);
    public void Error(string source, string message, Exception? ex = null)
    {
        var fullMessage = ex is null ? message : $"{message} | Detail: {ex.GetType().Name}: {ex.Message}";
        Write("ERROR", source, fullMessage);
    }

    public void Write(string level, string source, string message, string? additionalLogFile = null)
    {
        var entry = new LogEntry(DateTime.Now, level, source, message);
        lock (_fileLock)
        {
            try
            {
                Directory.CreateDirectory(DraGonPaths.LogsDirectory);
                File.AppendAllText(DraGonPaths.MainLogFilePath, entry.Formatted + Environment.NewLine, Encoding.UTF8);

                if (!string.IsNullOrWhiteSpace(additionalLogFile))
                {
                    File.AppendAllText(additionalLogFile, entry.Formatted + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Ignore logging IO errors so application does not crash
            }
        }

        EntryWritten?.Invoke(entry);
    }

    public IReadOnlyList<string> ReadRecentLines(int maxLines = 150)
    {
        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(DraGonPaths.MainLogFilePath))
                {
                    return [];
                }

                var allLines = File.ReadAllLines(DraGonPaths.MainLogFilePath, Encoding.UTF8);
                return allLines.TakeLast(maxLines).ToList();
            }
            catch
            {
                return [];
            }
        }
    }
}
