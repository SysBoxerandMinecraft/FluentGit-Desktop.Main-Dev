using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentGit.Services;

namespace FluentGit.Views;

public sealed partial class ClonePage : Page
{
    private const string TAG = "ClonePage";
    private CancellationTokenSource? _cts;
    private bool _isCloning = false;

    private string? _selectedBranch = null;
    private string _lastFetchedUrl = "";
    private bool _isFetchingBranches = false;
    private DispatcherQueueTimer? _urlDebounceTimer;
    private int _totalCommits = -1;

    public ClonePage()
    {
        InitializeComponent();
        BranchDropdown.Content = Loc.GetString("CloneDefaultBranch");
        BranchHint.Text = Loc.GetString("CloneHintDefault");
    }

    private void OnUrlTextChanged(object sender, TextChangedEventArgs e)
    {
        WarningBar.IsOpen = false;
        string currentUrl = UrlBox.Text.Trim();

        BranchDropdown.IsEnabled = false;
        DepthCombo.IsEnabled = false;

        if (string.IsNullOrEmpty(currentUrl))
        {
            BranchDropdown.Content = Loc.GetString("CloneDefaultBranch");
            BranchHint.Text = Loc.GetString("CloneHintDefault");
            _lastFetchedUrl = "";
            _selectedBranch = null;
            _totalCommits = -1;
            return;
        }

        BranchDropdown.Content = Loc.GetString("CloneChecking");
        BranchHint.Text = Loc.GetString("CloneHintChecking");

        if (_urlDebounceTimer == null)
        {
            _urlDebounceTimer = DispatcherQueue.CreateTimer();
            _urlDebounceTimer.Interval = TimeSpan.FromMilliseconds(800);
            _urlDebounceTimer.IsRepeating = false;
            _urlDebounceTimer.Tick += async (_, _) =>
            {
                string url = UrlBox.Text.Trim();
                if (string.IsNullOrEmpty(url))
                {
                    BranchDropdown.IsEnabled = false;
                    DepthCombo.IsEnabled = false;
                    BranchDropdown.Content = Loc.GetString("CloneDefaultBranch");
                    BranchHint.Text = Loc.GetString("CloneHintDefault");
                    _lastFetchedUrl = "";
                    _totalCommits = -1;
                    return;
                }

                if (url == _lastFetchedUrl)
                {
                    BranchDropdown.IsEnabled = true;
                    DepthCombo.IsEnabled = true;
                    return;
                }

                await FetchBranchesAsync(url);
            };
        }

        _urlDebounceTimer.Stop();
        _urlDebounceTimer.Start();
    }

    private async Task FetchBranchesAsync(string url)
    {
        if (_isFetchingBranches) return;
        _isFetchingBranches = true;

        try
        {
            var settings = SettingsService.Load();
            string? gitPath = settings.GitPath;
            if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath))
            {
                BranchHint.Text = Loc.GetString("CloneErrGitPath");
                return;
            }

            BranchMenu.Items.Clear();
            BranchDropdown.Content = Loc.GetString("CloneLoading");
            BranchDropdown.IsEnabled = false;
            DepthCombo.IsEnabled = false;
            BranchHint.Text = $"{Loc.GetString("CloneLoading")} {url}...";

            var branches = await GitService.GetRemoteBranchesAsync(gitPath, url);
            _lastFetchedUrl = url;
            _totalCommits = await GitService.GetRemoteCommitCountAsync(url);

            BranchMenu.Items.Clear();

            var defaultItem = new MenuFlyoutItem { Text = $"({Loc.GetString("CloneDefaultBranch")})" };
            defaultItem.Click += (_, _) => SelectBranch(null, Loc.GetString("CloneDefaultBranch"));
            BranchMenu.Items.Add(defaultItem);

            if (branches.Count > 0)
            {
                BranchMenu.Items.Add(new MenuFlyoutSeparator());

                foreach (var b in branches)
                {
                    var item = new MenuFlyoutItem { Text = b };
                    string captured = b;
                    item.Click += (_, _) => SelectBranch(captured, captured);
                    BranchMenu.Items.Add(item);
                }

                BranchHint.Text = string.Format(Loc.GetString("CloneLoadedBranches"), branches.Count);
            }
            else
            {
                BranchMenu.Items.Add(new MenuFlyoutSeparator());
                var emptyItem = new MenuFlyoutItem { Text = "()" };
                emptyItem.IsEnabled = false;
                BranchMenu.Items.Add(emptyItem);

                BranchHint.Text = Loc.GetString("CloneNoBranches");
            }

            BranchDropdown.IsEnabled = true;
            DepthCombo.IsEnabled = true;

            if (_selectedBranch != null && branches.Contains(_selectedBranch, StringComparer.OrdinalIgnoreCase))
            {
                BranchDropdown.Content = _selectedBranch;
            }
            else
            {
                _selectedBranch = null;
                BranchDropdown.Content = Loc.GetString("CloneDefaultBranch");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"加载分支失败: {ex.Message}");
            BranchDropdown.Content = Loc.GetString("CloneDefaultBranch");
            BranchDropdown.IsEnabled = true;
            DepthCombo.IsEnabled = true;
            BranchHint.Text = Loc.GetString("CloneNoBranches");
        }
        finally
        {
            _isFetchingBranches = false;
        }
    }

    private void SelectBranch(string? branch, string display)
    {
        _selectedBranch = branch;
        BranchDropdown.Content = display;
        AppLogger.Info(TAG, $"选择分支: {branch ?? "(默认)"}");
    }

    private void OnDepthSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
    {
        string text = args.Text?.Trim() ?? "";
        int depth = 0;

        if (int.TryParse(text, out int n) && n >= 0)
        {
            depth = n;
            sender.Text = n == 0
                ? Loc.GetString("CloneDepthFull.Content")
                : string.Format(Loc.GetString("CloneRecentCommits"), n);
        }
        else
        {
            sender.Text = Loc.GetString("CloneDepthFull.Content");
            depth = 0;
        }

        args.Handled = true;
        CheckDepthWarning(depth);
    }

    private void CheckDepthWarning(int depth)
    {
        if (depth <= 0 || _totalCommits < 0)
        {
            WarningBar.IsOpen = false;
            return;
        }

        if (depth > _totalCommits)
        {
            WarningBar.Title = Loc.GetString("CloneWarningDepthTitle");
            WarningBar.Message = string.Format(Loc.GetString("CloneWarningDepthMsg"), depth, _totalCommits);
            WarningBar.Severity = InfoBarSeverity.Warning;
            WarningBar.IsOpen = true;
        }
        else
        {
            WarningBar.IsOpen = false;
        }
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var folder = await picker.PickSingleFolderAsync();
            if (folder != null) PathBox.Text = folder.Path;
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"浏览目录异常: {ex.Message}");
        }
    }

    private async void OnStartClick(object sender, RoutedEventArgs e)
    {
        if (_isCloning) return;

        var settings = SettingsService.Load();
        string? gitPath = settings.GitPath;
        if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath))
        {
            await ShowErrorAsync(Loc.GetString("CloneErrGitPath"));
            return;
        }

        string url = UrlBox.Text.Trim();
        string targetPath = PathBox.Text.Trim();

        int depth = 0;
        string depthText = DepthCombo.Text?.Trim() ?? "";

        if (int.TryParse(depthText, out int customDepth) && customDepth >= 0)
        {
            depth = customDepth;
        }
        else if (DepthCombo.SelectedItem is ComboBoxItem depthItem &&
                 depthItem.Tag is string depthTag &&
                 int.TryParse(depthTag, out int parsedDepth) && parsedDepth >= 0)
        {
            depth = parsedDepth;
        }

        CheckDepthWarning(depth);

        if (string.IsNullOrEmpty(url))
        {
            await ShowErrorAsync(Loc.GetString("CloneErrUrl"));
            return;
        }

        if (string.IsNullOrEmpty(targetPath))
        {
            await ShowErrorAsync(Loc.GetString("CloneErrPath"));
            return;
        }

        _isCloning = true;
        _cts = new CancellationTokenSource();

        UrlBox.IsEnabled = false;
        PathBox.IsEnabled = false;
        BranchDropdown.IsEnabled = false;
        DepthCombo.IsEnabled = false;
        BrowseButton.IsEnabled = false;
        StartButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;
        StatusText.Visibility = Visibility.Visible;
        StatusText.Text = Loc.GetString("CloneStatusCloning");

        try
        {
            var progress = new Action<int, string>((percent, msg) =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    ProgressBar.Value = percent;
                    StatusText.Text = msg;
                });
            });

            int? depthParam = depth > 0 ? depth : null;
            string? branchParam = string.IsNullOrEmpty(_selectedBranch) ? null : _selectedBranch;

            var success = await Task.Run(() =>
                GitService.CloneRepositoryWithProgress(
                    gitPath, url, targetPath, progress, _cts.Token,
                    depthParam, branchParam));

            if (success)
            {
                AppState.SetRepository(targetPath);
                AppLogger.OK(TAG, $"克隆成功: {targetPath}");

                StatusText.Text = string.Format(Loc.GetString("CloneStatusSuccess"), targetPath);
                Frame.Navigate(typeof(RepoPage));
            }
            else
            {
                StatusText.Text = Loc.GetString("CloneStatusFailed");
                ResetUI();
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = Loc.GetString("CloneStatusCancelled");
            AppLogger.Warning(TAG, "克隆被用户取消");
            ResetUI();
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"克隆异常: {ex.Message}");
            StatusText.Text = $"{Loc.GetString("CloneStatusFailed")}: {ex.Message}";
            ResetUI();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (_isCloning && _cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
        else
        {
            Frame.Navigate(typeof(RepoPage));
        }
    }

    private void ResetUI()
    {
        _isCloning = false;
        _cts?.Dispose();
        _cts = null;

        UrlBox.IsEnabled = true;
        PathBox.IsEnabled = true;
        BrowseButton.IsEnabled = true;
        StartButton.IsEnabled = true;
        CancelButton.IsEnabled = false;

        bool urlChecked = !string.IsNullOrEmpty(_lastFetchedUrl);
        BranchDropdown.IsEnabled = urlChecked;
        DepthCombo.IsEnabled = urlChecked;
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = Loc.GetString("CommonInfo"),
            Content = message,
            CloseButtonText = Loc.GetString("CommonOK"),
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}