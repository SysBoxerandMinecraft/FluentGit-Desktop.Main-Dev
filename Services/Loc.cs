using Microsoft.Windows.ApplicationModel.Resources;

namespace FluentGit.Services;

public static class Loc
{
    private static readonly ResourceManager _manager = new();
    private static readonly ResourceContext _context = _manager.CreateResourceContext();

    public static string GetString(string key)
    {
        try 
        { 
            // WinUI 3 MRT 底层规范：根节点是 Resources，层级用 / 分隔
            // 比如 FileOpenTerminal.Text -> Resources/FileOpenTerminal/Text
            string mrtKey = key.Replace('.', '/');
            string fullKey = $"Resources/{mrtKey}";
            
            var res = _manager.MainResourceMap.GetValue(fullKey, _context);
            if (res != null && !string.IsNullOrEmpty(res.ValueAsString))
            {
                return res.ValueAsString;
            }
        }
        catch 
        {
            // 忽略异常
        }
        return key;
    }
}