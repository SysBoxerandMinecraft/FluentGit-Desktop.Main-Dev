using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentGit.Services;
using Windows.ApplicationModel.DataTransfer;

namespace FluentGit.Views;

public class TreeItemModel
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsFile { get; set; }
    public ImageSource? IconSource { get; set; }
}

public sealed partial class FilePage : Page
{
    private const string TAG = "FilePage";
    private static readonly ConcurrentDictionary<string, ImageSource> _iconCache = new();

    private string[] _activeSkipFolders = new[] { ".git" };

    private static readonly string[] SkipFolders =
    {
        ".git", ".svn", ".hg",
        "bin", "obj", "packages", "artifacts", "TestResults", ".vs",
        "__pycache__", ".pytest_cache", ".mypy_cache", ".ruff_cache",
        ".venv", "venv", "env", ".tox", ".eggs",
        ".ipynb_checkpoints", "__pypackages__",
        "node_modules", ".next", ".nuxt", ".turbo", ".vercel",
        "dist", "build", "out", "coverage", ".nyc_output",
        ".pnpm-store", ".yarn",
        "target", ".gradle", ".mvn",
        ".cargo",
        "vendor",
        "composer.phar",
        ".bundle", "log", "tmp", "pkg",
        "CMakeFiles", "cmake_install.cmake",
        "DerivedData",
        ".idea", ".vscode", ".fleet", ".history", ".settings",
        ".cache", ".temp", "temp", "logs",
        "$RECYCLE.BIN",
    };

    private DateTime _lastClickTime = DateTime.MinValue;
    private string? _lastClickedPath = null;

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    public FilePage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadDirectoryTreeFromState();
    }

    private void LoadDirectoryTreeFromState()
    {
        var repoPath = AppState.CurrentRepoPath;

        if (string.IsNullOrEmpty(repoPath) || !Directory.Exists(repoPath))
        {
            NoRepoText.Visibility = Visibility.Visible;
            DirectoryTreeView.Visibility = Visibility.Collapsed;
            DirectoryTreeView.RootNodes.Clear();
            return;
        }

        NoRepoText.Visibility = Visibility.Collapsed;
        DirectoryTreeView.Visibility = Visibility.Visible;
        LoadDirectoryTree(repoPath);
    }

    private string[] GetActiveSkipFolders()
    {
        var settings = SettingsService.Load();
        if (!settings.FilterBuildArtifacts)
            return new[] { ".git" };

        return SkipFolders;
    }

    // ========== 内置图标 ==========
    private static string IconRoot =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "File_Icon");

    private static ImageSource? GetIcon(string iconFile)
    {
        if (_iconCache.TryGetValue(iconFile, out var cached))
            return cached;

        try
        {
            string full = Path.Combine(IconRoot, iconFile);
            if (File.Exists(full))
            {
                var img = new BitmapImage(new Uri(full));
                _iconCache[iconFile] = img;
                return img;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"加载图标异常 {iconFile}: {ex.Message}");
        }
        return null;
    }

    // ========== 目录树（懒加载） ==========
    private void LoadDirectoryTree(string rootPath)
    {
        AppLogger.Info(TAG, $"加载根节点: {rootPath}");
        var sw = Stopwatch.StartNew();

        _activeSkipFolders = GetActiveSkipFolders();
        DirectoryTreeView.RootNodes.Clear();

        try
        {
            var rootName = Path.GetFileName(rootPath);
            if (string.IsNullOrEmpty(rootName)) rootName = rootPath;

            var rootModel = new TreeItemModel
            {
                // 使用格式化字符串，翻译为 "{0}（仓库）"
                Name = string.Format(App.Localizer.GetLocalizedString("FileRepoRoot"), rootName),
                FullPath = rootPath,
                IsFile = false,
                IconSource = GetIcon("Full_Folder.png")
            };

            var rootNode = new TreeViewNode
            {
                Content = rootModel,
                IsExpanded = true
            };

            DirectoryTreeView.RootNodes.Add(rootNode);

            PopulateChildren(rootNode, rootPath);

            sw.Stop();
            AppLogger.OK(TAG, $"根节点加载完成: {rootNode.Children.Count} 子项, {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"加载目录树异常: {ex.Message}");
        }
    }

    private void PopulateChildren(TreeViewNode parent, string path)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
            {
                string name = Path.GetFileName(dir);
                if (_activeSkipFolders.Contains(name, StringComparer.OrdinalIgnoreCase))
                    continue;

                bool hasAnyChild = HasAnyVisibleChild(dir);

                var model = new TreeItemModel
                {
                    Name = name,
                    FullPath = dir,
                    IsFile = false,
                    IconSource = GetIcon(hasAnyChild ? "Full_Folder.png" : "Empty_Folder.png")
                };

                var node = new TreeViewNode
                {
                    Content = model,
                    HasUnrealizedChildren = hasAnyChild
                };

                parent.Children.Add(node);
            }

            foreach (var file in Directory.GetFiles(path).OrderBy(f => f))
            {
                var model = new TreeItemModel
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    IsFile = true,
                    IconSource = GetIcon("File.png")
                };

                parent.Children.Add(new TreeViewNode { Content = model });
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning(TAG, $"PopulateChildren 失败: {path} - {ex.Message}");
        }
    }

    private bool HasAnyVisibleChild(string dir)
    {
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
            {
                string name = Path.GetFileName(entry);
                if (!_activeSkipFolders.Contains(name, StringComparer.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    // ========== 展开时懒加载下一层 ==========
    private void DirectoryTreeView_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        var node = args.Node;
        if (node == null) return;
        if (node.Content is not TreeItemModel model) return;
        if (model.IsFile) return;

        if (node.HasUnrealizedChildren)
        {
            var sw = Stopwatch.StartNew();
            node.Children.Clear();
            PopulateChildren(node, model.FullPath);
            node.HasUnrealizedChildren = false;

            sw.Stop();
            AppLogger.Info(TAG, $"懒加载: {model.Name} → {node.Children.Count} 项, {sw.ElapsedMilliseconds}ms");
        }
    }

    // ========== 双击打开 ==========
    private void DirectoryTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        TreeItemModel? model = ExtractModel(args.InvokedItem);
        if (model == null) return;

        var now = DateTime.Now;
        bool isDoubleClick =
            (now - _lastClickTime).TotalMilliseconds < GetDoubleClickTime() &&
            model.FullPath == _lastClickedPath;

        _lastClickTime = now;
        _lastClickedPath = model.FullPath;

        if (isDoubleClick && model.IsFile && File.Exists(model.FullPath))
        {
            OpenFile(model.FullPath);
        }
    }

    // ========== 右键菜单 ==========
    private void DirectoryTreeView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var element = e.OriginalSource as FrameworkElement;
        if (element == null) return;

        var tvi = FindAncestor<TreeViewItem>(element);
        if (tvi == null) return;

        var node = DirectoryTreeView.NodeFromContainer(tvi);
        if (node?.Content is not TreeItemModel model) return;

        DirectoryTreeView.SelectedNode = node;

        var menu = BuildContextMenu(model);
        menu.ShowAt(element, e.GetPosition(element));
        e.Handled = true;
    }

    private MenuFlyout BuildContextMenu(TreeItemModel model)
    {
        var menu = new MenuFlyout();

        if (model.IsFile)
        {
            var openItem = new MenuFlyoutItem
            {
                Text = App.Localizer.GetLocalizedString("FileOpen"),
                Icon = new SymbolIcon(Symbol.OpenFile)
            };
            openItem.Click += (_, _) => OpenFile(model.FullPath);
            menu.Items.Add(openItem);
        }
        else
        {
            var terminalItem = new MenuFlyoutItem
            {
                Text = App.Localizer.GetLocalizedString("FileOpenTerminal"),
                Icon = new SymbolIcon(Symbol.Play)
            };
            terminalItem.Click += (_, _) => OpenTerminal(model.FullPath);
            menu.Items.Add(terminalItem);

            var explorerItem = new MenuFlyoutItem
            {
                Text = App.Localizer.GetLocalizedString("FileOpenExplorer"),
                Icon = new SymbolIcon(Symbol.Folder)
            };
            explorerItem.Click += (_, _) => OpenExplorer(model.FullPath);
            menu.Items.Add(explorerItem);
        }

        menu.Items.Add(new MenuFlyoutSeparator());

        var copyPathItem = new MenuFlyoutItem
        {
            Text = App.Localizer.GetLocalizedString("FileCopyPath"),
            Icon = new SymbolIcon(Symbol.Copy)
        };
        copyPathItem.Click += (_, _) => CopyToClipboard(model.FullPath);
        menu.Items.Add(copyPathItem);

        var copyNameItem = new MenuFlyoutItem
        {
            Text = App.Localizer.GetLocalizedString("FileCopyName"),
            Icon = new SymbolIcon(Symbol.Copy)
        };
        copyNameItem.Click += (_, _) => CopyToClipboard(model.Name);
        menu.Items.Add(copyNameItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        var refreshItem = new MenuFlyoutItem
        {
            Text = App.Localizer.GetLocalizedString("FileRefreshTree"),
            Icon = new SymbolIcon(Symbol.Refresh)
        };
        refreshItem.Click += (_, _) =>
        {
            var repo = AppState.CurrentRepoPath;
            if (!string.IsNullOrEmpty(repo))
                LoadDirectoryTree(repo);
        };
        menu.Items.Add(refreshItem);

        return menu;
    }
    // ========== 右键菜单具体动作 ==========
    private void OpenFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            AppLogger.OK(TAG, $"打开文件: {path}");
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"打开文件失败: {ex.Message}");
        }
    }

    private void OpenTerminal(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return;

            string wtPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\WindowsApps\wt.exe");

            if (File.Exists(wtPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = wtPath,
                    Arguments = $"-d \"{folderPath}\"",
                    UseShellExecute = true
                });
                AppLogger.OK(TAG, $"WT 打开: {folderPath}");
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/K cd /d \"{folderPath}\"",
                    UseShellExecute = true
                });
                AppLogger.OK(TAG, $"cmd 打开: {folderPath}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"打开终端失败: {ex.Message}");
        }
    }

    private void OpenExplorer(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return;
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = true
            });
            AppLogger.OK(TAG, $"资源管理器打开: {folderPath}");
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"打开资源管理器失败: {ex.Message}");
        }
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
            AppLogger.OK(TAG, $"已复制: {text}");
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"复制失败: {ex.Message}");
        }
    }

    // ========== 工具方法 ==========
    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        var cur = obj;
        while (cur != null)
        {
            if (cur is T t) return t;
            cur = VisualTreeHelper.GetParent(cur);
        }
        return null;
    }

    private TreeItemModel? ExtractModel(object? obj)
    {
        if (obj is TreeViewNode node && node.Content is TreeItemModel m)
            return m;

        if (obj is TreeItemModel m2)
            return m2;

        return null;
    }
}