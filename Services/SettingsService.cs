using System;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace FluentGit.Services;

public class AppSettings
{
    public string? GitPath { get; set; }
    public string? GitHash { get; set; }
    public string? AppTheme { get; set; }
    public bool FilterBuildArtifacts { get; set; } = true;
    public string? BackdropType { get; set; }
    public string? Language { get; set; } = "zh-CN"; 
}

public static class SettingsService
{
    private const string TAG = "SettingsService";

    // 修改这里：直接放在程序运行目录下的 Setting 文件夹
    private static readonly string _settingsPath = Path.Combine(
        AppContext.BaseDirectory, "Setting", "settings.json"
    );

    static SettingsService()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"创建设置目录失败: {ex.Message}");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    if (string.IsNullOrEmpty(settings.Language))
                        settings.Language = "zh-CN";
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"读取设置失败: {ex.Message}");
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

        for (int i = 0; i < 3; i++)
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(_settingsPath, json);
                AppLogger.OK(TAG, $"设置已保存到 {_settingsPath}");
                return; 
            }
            catch (Exception ex)
            {
                AppLogger.Warning(TAG, $"尝试保存设置失败 (第 {i + 1} 次): {ex.Message}");
                Thread.Sleep(100); 
            }
        }
        AppLogger.Error(TAG, "保存设置彻底失败，文件可能被锁定。");
    }
}