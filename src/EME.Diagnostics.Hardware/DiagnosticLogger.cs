using System.Runtime.CompilerServices;

namespace EME.Diagnostics.Hardware;

public static class DiagnosticLogger
{
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EME_Diagnostics", "diag.log");

    private static readonly object _lock = new();

    public static void Log(string message, [CallerMemberName] string member = "", [CallerFilePath] string file = "")
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_logPath);
                if (dir != null) Directory.CreateDirectory(dir);
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var caller = Path.GetFileNameWithoutExtension(file);
                File.AppendAllText(_logPath, $"[{timestamp}][{caller}.{member}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            try { if (File.Exists(_logPath)) File.Delete(_logPath); }
            catch { }
        }
    }
}
