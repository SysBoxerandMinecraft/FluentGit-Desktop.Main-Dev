using Microsoft.UI.Xaml;
using System;
using FluentGit.Services;
using Windows.Globalization;

namespace FluentGit;

public partial class App : Application
{
    private Window? _window;
    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        try
        {
            var settings = SettingsService.Load();
            string targetLang = settings.Language ?? "zh-CN";
            
            // 设置强制覆盖语言
            ApplicationLanguages.PrimaryLanguageOverride = targetLang;
            
            System.Diagnostics.Debug.WriteLine($"[FluentGit] Language Override Set To: {targetLang}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"应用语言设置失败: {ex.Message}");
        }

        InitializeComponent();

        UnhandledException += OnUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            CrashLogger.Log("AppDomain", e.ExceptionObject as Exception);
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLogger.Log("TaskScheduler", e.Exception);
            e.SetObserved();
        };
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        if (e.Exception is ArgumentException argEx &&
            argEx.Message.Contains("FontFamily", StringComparison.OrdinalIgnoreCase))
        {
            AppLogger.Warning("App", $"忽略已知的 FontFamily 异常: {e.Message}");
            e.Handled = true;
            return;
        }

        CrashLogger.Log("UI", e.Exception);
        AppLogger.Error("App", $"未处理异常: {e.Message}");

        if (e.Exception is OutOfMemoryException or StackOverflowException)
        {
            e.Handled = false;
        }
        else
        {
            e.Handled = true;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        MainWindow = window;
        _window = window;
        window.Activate();
    }
}