using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Threading.Tasks;
using FluentGit.Services;
using Windows.ApplicationModel.DataTransfer;

namespace FluentGit.Views;

public sealed partial class HistoryPage : Page
{
    private const string TAG = "HistoryPage";
    private bool _isLoading = false;
    private static readonly FontFamily UiFont = new FontFamily("Segoe UI Variable,Segoe UI");
    private static readonly FontFamily MonoFont = new FontFamily("Consolas,Cascadia Mono,Courier New");

    public HistoryPage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => _ = LoadLogAsync();

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = LoadLogAsync();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await LoadLogAsync();

    private async Task LoadLogAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        try
        {
            var repoPath = AppState.CurrentRepoPath;
            if (string.IsNullOrEmpty(repoPath) || !Directory.Exists(repoPath)) { NoRepoText.Visibility = Visibility.Visible; EmptyText.Visibility = Visibility.Collapsed; LogListView.Visibility = Visibility.Collapsed; return; }

            var settings = SettingsService.Load();
            string? gitPath = settings.GitPath;
            if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath))
            {
                NoRepoText.Text = App.Localizer.GetLocalizedString("HistoryErrGitPath");
                NoRepoText.Visibility = Visibility.Visible; EmptyText.Visibility = Visibility.Collapsed; LogListView.Visibility = Visibility.Collapsed;
                return;
            }

            LoadingRing.Visibility = Visibility.Visible; LoadingRing.IsActive = true; RefreshButton.IsEnabled = false;
            var commits = await Task.Run(() => GitService.GetLog(gitPath, repoPath));

            if (commits == null || commits.Count == 0) { NoRepoText.Visibility = Visibility.Collapsed; EmptyText.Visibility = Visibility.Visible; LogListView.Visibility = Visibility.Collapsed; }
            else { NoRepoText.Visibility = Visibility.Collapsed; EmptyText.Visibility = Visibility.Collapsed; LogListView.Visibility = Visibility.Visible; LogListView.ItemsSource = commits; }
        }
        catch (Exception ex) { AppLogger.Error(TAG, $"LoadLogAsync 异常: {ex.Message}"); }
        finally
        {
            LoadingRing.IsActive = false; LoadingRing.Visibility = Visibility.Collapsed; RefreshButton.IsEnabled = true; _isLoading = false;
        }
    }

    private void OnItemRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GitCommit commit) LogListView.SelectedItem = commit;
    }

    private async void OnCommitItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not GitCommit commit) return;
        await ShowCommitDetailAsync(commit);
    }

    private void OnHashClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hash) CopyToClipboard(hash, App.Localizer.GetLocalizedString("HistoryDetailHash"));
    }

    private void OnCopyHashClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string hash) CopyToClipboard(hash, App.Localizer.GetLocalizedString("HistoryDetailHash"));
    }

    private void OnCopyShortHashClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string shortHash) CopyToClipboard(shortHash, App.Localizer.GetLocalizedString("HistoryDetailShortHash"));
    }

    private void OnCopyMessageClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string message) CopyToClipboard(message, App.Localizer.GetLocalizedString("HistoryDetailMsg"));
    }

    private async void OnViewDetailClick(object sender, RoutedEventArgs e)
    {
        if (LogListView.SelectedItem is GitCommit commit) await ShowCommitDetailAsync(commit);
    }

    private async Task ShowCommitDetailAsync(GitCommit commit)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(BuildDetailRow(App.Localizer.GetLocalizedString("HistoryDetailHash"), commit.Hash, isMonospace: true));
        panel.Children.Add(BuildDetailRow(App.Localizer.GetLocalizedString("HistoryDetailShortHash"), commit.ShortHash, isMonospace: true));
        panel.Children.Add(BuildSeparator());
        panel.Children.Add(BuildDetailRow(App.Localizer.GetLocalizedString("HistoryDetailAuthor"), commit.Author));
        panel.Children.Add(BuildDetailRow(App.Localizer.GetLocalizedString("HistoryDetailEmail"), commit.Email));
        panel.Children.Add(BuildDetailRow(App.Localizer.GetLocalizedString("HistoryDetailTime"), commit.DateDisplay));
        panel.Children.Add(BuildDetailRow(App.Localizer.GetLocalizedString("HistoryDetailRelative"), commit.RelativeTime));
        panel.Children.Add(BuildSeparator());

        panel.Children.Add(new TextBlock { Text = App.Localizer.GetLocalizedString("HistoryDetailMsg"), FontSize = 12, FontFamily = UiFont, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        panel.Children.Add(new TextBlock { Text = commit.Message, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, FontFamily = UiFont });

        var dialog = new ContentDialog
        {
            Title = App.Localizer.GetLocalizedString("HistoryDetailTitle"),
            Content = new ScrollViewer { Content = panel, MaxHeight = 400, HorizontalScrollMode = ScrollMode.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
            PrimaryButtonText = App.Localizer.GetLocalizedString("HistoryCopyHash"),
            CloseButtonText = App.Localizer.GetLocalizedString("CommonClose"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary) CopyToClipboard(commit.Hash, App.Localizer.GetLocalizedString("HistoryDetailHash"));
    }

    private static StackPanel BuildDetailRow(string label, string value, bool isMonospace = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Width = 60, FontSize = 12, FontFamily = UiFont, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], VerticalAlignment = VerticalAlignment.Top });
        row.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, FontFamily = isMonospace ? MonoFont : UiFont, FontSize = 13 });
        return row;
    }

    private static Border BuildSeparator()
    {
        return new Border { Height = 1, Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], Margin = new Thickness(0, 4, 0, 4) };
    }

    private void CopyToClipboard(string text, string label)
    {
        try
        {
            var dp = new DataPackage(); dp.SetText(text); Clipboard.SetContent(dp);
        }
        catch (Exception ex) { AppLogger.Error(TAG, $"复制失败: {ex.Message}"); }
    }
}