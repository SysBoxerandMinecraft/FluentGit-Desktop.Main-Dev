using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace FluentGit.Services;

public static class AppLogger
{
    private const string Prefix = "[FluentGit]";

    // 修改这里：写到程序目录 Setting\logs 下
    private static readonly string LogDir = Path.Combine(AppContext.BaseDirectory, "Setting", "logs");
    private static readonly string LogFile = Path.Combine(LogDir, "app.log");

    private static readonly object _lock = new();
    private static bool _fileLoggingEnabled = true;

    static AppLogger()
    {
        try
        {
            Directory.CreateDirectory(LogDir);
        }
        catch
        {
            _fileLoggingEnabled = false;
        }
    }

    public static void OK(string tag, string message)      => Write("OK     ", tag, message);
    public static void Warning(string tag, string message) => Write("Warning", tag, message);
    public static void Error(string tag, string message)   => Write("Error  ", tag, message);
    public static void Info(string tag, string message)    => Write("Info   ", tag, message);

    private static void Write(string level, string tag, string message)
    {
        string line = $"{Prefix} [{level}] [{tag}] {message}";

        // 输出到 VS 调试窗口
        Debug.WriteLine(line);
        // 输出到终端控制台
        Console.WriteLine(line);

        if (!_fileLoggingEnabled) return;

        try
        {
            string timestamped = $"[{DateTime.Now:HH:mm:ss.fff}] {line}\n";
            lock (_lock)
            {
                File.AppendAllText(LogFile, timestamped, Encoding.UTF8);
            }
        }
        catch
        {
            // 忽略日志写入失败
        }
    }
}