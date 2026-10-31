using Microsoft.UI.Xaml;
using System;
using System.IO;
using FluentGit.Services;
using Windows.Globalization;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace FluentGit;

public partial class App : Application
{
    private Window? _window;
    public static MainWindow? MainWindow { get; private set; }

    /// <summary>
    /// WinUI3Localizer 的全局实例（未初始化时是 NullLocalizer，不会抛异常）。
    /// 属性名与 WinUI3Localizer.Localizer 静态类同名，本类内部必须用 global:: 全限定。
    /// </summary>
    public static ILocalizer Localizer => global::WinUI3Localizer.Localizer.Get();

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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await InitializeLocalizerAsync();

        var window = new MainWindow();
        MainWindow = window;
        _window = window;
        window.Activate();
    }

    private static async Task InitializeLocalizerAsync()
    {
        try
        {
            // 非打包应用：使用 exe 旁边的 Strings 文件夹（csproj 已把 Strings\**\*.resw 复制到输出目录）
            string stringsFolderPath = Path.Combine(AppContext.BaseDirectory, "Strings");
            if (Directory.Exists(stringsFolderPath) is false)
            {
                AppLogger.Error("App", $"Localizer 初始化失败: 找不到语言资源目录 {stringsFolderPath}");
                return;
            }

            ILocalizer localizer = await new LocalizerBuilder()
                .AddStringResourcesFolderForLanguageDictionaries(stringsFolderPath)
                .SetOptions(options =>
                {
                    options.DefaultLanguage = "zh-CN";
                })
                .Build();

            // Build() 内部已经把语言设为 DefaultLanguage，这里再切到用户保存的语言
            var settings = SettingsService.Load();
            if (string.IsNullOrEmpty(settings.Language) is false)
            {
                await localizer.SetLanguage(settings.Language);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("App", $"Localizer 初始化失败: {ex.Message}");
        }
    }
}
