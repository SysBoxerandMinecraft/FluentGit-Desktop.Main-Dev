namespace FluentGit.Services;

/// <summary>
/// 便捷入口：转发到 WinUI3Localizer 的全局实例（见 <see cref="App.Localizer"/>）。
/// 新代码建议直接使用 <c>App.Localizer.GetLocalizedString("Key")</c>。
/// </summary>
public static class Loc
{
    public static string GetString(string key) => App.Localizer.GetLocalizedString(key);
}
