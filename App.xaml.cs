using Microsoft.UI.Xaml;
using System;
using FluentGit.Services;
using Windows.Globalization;
using System.Globalization;
using System.Threading;

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

            // 1. 设置应用首选语言
            ApplicationLanguages.PrimaryLanguageOverride = targetLang;

            // 2. 暴力强制修改当前线程的语言区域（对付 WinUI 3 的 XAML 资源加载）
            var culture = new CultureInfo(targetLang);
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;

            System.Diagnostics.Debug.WriteLine($"[FluentGit] 语言已强制切换为: {targetLang}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"应用语言设置失败: {ex.Message}");
        }

        // 必须在此之后初始化组件
        InitializeComponent();

        UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        if (e.Exception is ArgumentException argEx && argEx.Message.Contains("FontFamily", StringComparison.OrdinalIgnoreCase))
        {
            e.Handled = true;
            return;
        }
        CrashLogger.Log("UI", e.Exception);
        AppLogger.Error("App", $"未处理异常: {e.Message}");
        e.Handled = true;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        MainWindow = window;
        _window = window;
        window.Activate();
    }
}