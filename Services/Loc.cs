using Microsoft.Windows.ApplicationModel.Resources;

namespace FluentGit.Services;

public static class Loc
{
    private static readonly ResourceLoader _loader = new();

    /// <summary>
    /// 从资源文件获取本地化字符串
    /// </summary>
    public static string GetString(string key)
    {
        try { return _loader.GetString(key); }
        catch { return key; }
    }
}