using Microsoft.UI.Xaml.Controls;

namespace FluentGit.Services;

public static class Toast
{
    public static void Success(string message, string title = "")
        => App.MainWindow?.ShowInfoBar(string.IsNullOrEmpty(title) ? App.Localizer.GetLocalizedString("CommonSuccess") : title, message, InfoBarSeverity.Success);

    public static void Warning(string message, string title = "")
        => App.MainWindow?.ShowInfoBar(string.IsNullOrEmpty(title) ? App.Localizer.GetLocalizedString("CommonWarning") : title, message, InfoBarSeverity.Warning);

    public static void Error(string message, string title = "")
        => App.MainWindow?.ShowInfoBar(string.IsNullOrEmpty(title) ? App.Localizer.GetLocalizedString("CommonError") : title, message, InfoBarSeverity.Error);

    public static void Info(string message, string title = "")
        => App.MainWindow?.ShowInfoBar(string.IsNullOrEmpty(title) ? App.Localizer.GetLocalizedString("CommonInfo") : title, message, InfoBarSeverity.Informational);

    public static void Show(string message, string title = "", InfoBarSeverity severity = InfoBarSeverity.Informational)
        => App.MainWindow?.ShowInfoBar(title, message, severity);
}