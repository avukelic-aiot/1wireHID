using System.Globalization;

namespace OneWireHID;

internal static class AppLogger
{
    private static readonly object Sync = new();

    public static string DocumentsPath =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public static string CsvPath =>
        Path.Combine(DocumentsPath, $"1wireHID-iButtons-{DateTime.Now:yyyy-MM-dd}.csv");

    public static string LogPath =>
        Path.Combine(DocumentsPath, $"1wireHID-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Info(string message) => WriteLog("INFO", message);

    public static void Warn(string message) => WriteLog("WARN", message);

    public static void Error(string message, Exception? exception = null)
    {
        WriteLog("ERROR", exception == null ? message : $"{message}: {exception}");
    }

    public static void IButtonTouched(string romHex, string source, bool forwarded)
    {
        lock (Sync)
        {
            EnsureCsvHeader();
            File.AppendAllText(
                CsvPath,
                string.Join(",", new[]
                {
                    Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)),
                    Csv(romHex),
                    Csv(source),
                    Csv("touch"),
                    Csv(forwarded ? "true" : "false")
                }) + Environment.NewLine);
        }
    }

    private static void WriteLog(string level, string message)
    {
        lock (Sync)
        {
            File.AppendAllText(
                LogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
        }
    }

    private static void EnsureCsvHeader()
    {
        if (File.Exists(CsvPath))
            return;

        File.WriteAllText(CsvPath, "timestamp,rom,source,event,forwarded" + Environment.NewLine);
    }

    private static string Csv(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
