using System.Runtime.CompilerServices;

namespace EME.Diagnostics.Networking;

public static class NetDiagnostics
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EMEDiagnostics", "network_trace.log");

    static NetDiagnostics()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        File.WriteAllText(LogPath, $"=== NETWORK TRACE STARTED {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\r\n");
    }

    public static void Log(string message, [CallerMemberName] string caller = "")
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] [{caller}] {message}\r\n");
        }
        catch { }
    }
}
