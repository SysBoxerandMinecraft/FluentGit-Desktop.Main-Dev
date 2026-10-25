using System;
using System.IO;
using System.Text;

namespace FluentGit.Services;

public static class CrashLogger
{
    // 修改这里
    private static readonly string LogDir = Path.Combine(AppContext.BaseDirectory, "Setting", "logs");

    private static readonly object _lock = new();

    public static void Log(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var file = Path.Combine(LogDir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");

            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] source={source}");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($"Runtime: {Environment.Version}");
            sb.AppendLine($"ProcessArch: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine($"Exception: {ex?.GetType().FullName ?? "(null)"}");
            sb.AppendLine($"Message: {ex?.Message ?? "(null)"}");
            sb.AppendLine("---------- StackTrace ----------");
            sb.AppendLine(ex?.ToString() ?? "(null)");
            sb.AppendLine();

            lock (_lock)
            {
                File.AppendAllText(file, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
        }
    }
}