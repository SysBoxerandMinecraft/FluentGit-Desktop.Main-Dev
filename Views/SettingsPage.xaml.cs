using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentGit.Services;
using FluentGit.Services.Search;

namespace FluentGit.Views;

public sealed partial class SettingsPage : Page
{
    private const string TAG = "SettingsPage";
    private bool _isUpdating = false;
    private readonly List<SearchEntry> _settings = new();

    public SettingsPage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildSearchIndex();
        var settings = SettingsService.Load();
        _isUpdating = true;

        string currentLang = settings.Language ?? "zh-CN";
        foreach (var obj in LanguageComboBox.Items)
        {
            if (obj is ComboBoxItem item && (item.Tag as string) == currentLang) { LanguageComboBox.SelectedItem = item; break; }
        }

        FilterArtifactsCheckBox.IsChecked = settings.FilterBuildArtifacts;
        string currentBackdrop = settings.BackdropType ?? "Default";
        if (currentBackdrop == "MicaAlt") currentBackdrop = "Default";

        foreach (var obj in BackdropComboBox.Items)
        {
            if (obj is ComboBoxItem item && (item.Tag as string) == currentBackdrop) { BackdropComboBox.SelectedItem = item; break; }
        }

        _isUpdating = false;
        UpdateBackdropHint(currentBackdrop);
        await AutoFindAndValidateGitAsync();
    }

    private void BuildSearchIndex()
    {
        _settings.Clear();
        _settings.Add(new SearchEntry { Title = App.Localizer.GetLocalizedString("SettingsSearchTitleLanguage"), Keywords = new[] { "语言", "language", "i18n" }, Item = ItemLanguage, Group = GroupLanguage });
        _settings.Add(new SearchEntry { Title = App.Localizer.GetLocalizedString("SettingsSearchTitleGit"), Keywords = new[] { "git", "path", "install" }, Item = ItemGitPath, Group = GroupGit });
        _settings.Add(new SearchEntry { Title = App.Localizer.GetLocalizedString("SettingsSearchTitleBackdrop"), Keywords = new[] { "背景", "效果", "mica", "acrylic" }, Item = ItemBackdrop, Group = GroupAppearance });
        _settings.Add(new SearchEntry { Title = App.Localizer.GetLocalizedString("SettingsSearchTitleFilter"), Keywords = new[] { "过滤", "编译", "bin", "obj" }, Item = ItemFilterArtifacts, Group = GroupTreeView });
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        ApplySearch(sender.Text.Trim());
    }

    private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is string title) { sender.Text = title; ApplySearch(title); }
    }

    private void OnSearchSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string title) sender.Text = title;
    }

    private void ApplySearch(string query)
    {
        var matchedTitles = SearchService.ApplyFilter(_settings, query, out bool hasAnyMatch);
        NoResultText.Visibility = hasAnyMatch ? Visibility.Collapsed : Visibility.Visible;
        SearchBox.ItemsSource = matchedTitles.Count > 0 ? matchedTitles : null;
    }

    private async void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (LanguageComboBox.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not string langCode) return;

        var settings = SettingsService.Load();
        if (settings.Language == langCode) return;

        settings.Language = langCode;
        SettingsService.Save(settings);

        // 实时切换语言：SetLanguage 会重新本地化所有通过 Uids.Uid 注册的控件
        try
        {
            await App.Localizer.SetLanguage(langCode);
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"切换语言失败: {ex.Message}");
        }

        // 刷新当前页面的搜索索引
        BuildSearchIndex();
    }

    private void OnFilterArtifactsChecked(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        var settings = SettingsService.Load(); settings.FilterBuildArtifacts = true; SettingsService.Save(settings);
        Toast.Success(App.Localizer.GetLocalizedString("SettingsToastFilterOn"), App.Localizer.GetLocalizedString("SettingsToastSettingsUpdated"));
    }

    private void OnFilterArtifactsUnchecked(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        var settings = SettingsService.Load(); settings.FilterBuildArtifacts = false; SettingsService.Save(settings);
        Toast.Info(App.Localizer.GetLocalizedString("SettingsToastFilterOff"), App.Localizer.GetLocalizedString("SettingsToastSettingsUpdated"));
    }

    private void OnBackdropChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (BackdropComboBox.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not string tag) return;

        var settings = SettingsService.Load(); settings.BackdropType = tag; SettingsService.Save(settings);
        App.MainWindow?.ApplyBackdrop(tag);
        UpdateBackdropHint(tag);

        string displayName = tag switch
        {
            "Default" => App.Localizer.GetLocalizedString("SettingsBackdropDefault"),
            "Mica"    => App.Localizer.GetLocalizedString("SettingsBackdropMica"),
            "Acrylic" => App.Localizer.GetLocalizedString("SettingsBackdropAcrylic"),
            "None"    => App.Localizer.GetLocalizedString("SettingsBackdropNone"),
            _         => tag
        };
        Toast.Success(string.Format(App.Localizer.GetLocalizedString("SettingsToastBackdropChanged"), displayName), App.Localizer.GetLocalizedString("SettingsToastAppearanceUpdated"));
    }

    private void UpdateBackdropHint(string tag)
    {
        string hint = tag switch
        {
            "Default" => MicaController.IsSupported() ? App.Localizer.GetLocalizedString("SettingsHintDefaultOk") : App.Localizer.GetLocalizedString("SettingsHintDefaultFail"),
            "Mica" => MicaController.IsSupported() ? App.Localizer.GetLocalizedString("SettingsHintMicaOk") : App.Localizer.GetLocalizedString("SettingsHintMicaFail"),
            "Acrylic" => DesktopAcrylicController.IsSupported() ? App.Localizer.GetLocalizedString("SettingsHintAcrylicOk") : App.Localizer.GetLocalizedString("SettingsHintAcrylicFail"),
            "None" => App.Localizer.GetLocalizedString("SettingsHintNone"),
            _ => ""
        };
        BackdropHint.Text = hint;
    }

    private async Task AutoFindAndValidateGitAsync()
    {
        LoadingRing.IsActive = true;
        try
        {
            var settings = SettingsService.Load();
            string? gitPath = settings.GitPath;
            if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath))
            {
                await Task.Delay(100);
                gitPath = await Task.Run(() => GitPathHelper.FindGitPath());
            }
            if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath))
            {
                UpdateSelectedPathDisplay(""); GitVersionDisplay.Visibility = Visibility.Collapsed;
                Toast.Warning(App.Localizer.GetLocalizedString("SettingsToastGitNotFound"));
                return;
            }
            if (!GitPathHelper.ValidateGitPath(gitPath))
            {
                UpdateSelectedPathDisplay(gitPath);
                await UpdateGitVersionAsync(gitPath);
                Toast.Error(App.Localizer.GetLocalizedString("SettingsToastGitInvalid"));
                return;
            }
            
            // 【关键修复】重新加载一次最新的 settings，防止覆盖用户刚切换的语言
            var latestSettings = SettingsService.Load(); 
            latestSettings.GitPath = gitPath;
            SettingsService.Save(latestSettings);

            UpdateSelectedPathDisplay(gitPath);
            await UpdateGitVersionAsync(gitPath);
            Toast.Success(string.Format(App.Localizer.GetLocalizedString("SettingsToastGitFound"), gitPath));
        }
        catch (Exception ex) { Toast.Error(string.Format(App.Localizer.GetLocalizedString("SettingsToastSearchError"), ex.Message)); }
        finally { LoadingRing.IsActive = false; }
    }

    private void UpdateSelectedPathDisplay(string path)
    {
        SelectedPathDisplay.Text = string.IsNullOrEmpty(path) ? App.Localizer.GetLocalizedString("SettingsGitNoPath") : string.Format(App.Localizer.GetLocalizedString("SettingsGitCurrentPath"), path);
    }

    private async Task UpdateGitVersionAsync(string? gitPath)
    {
        if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath)) { GitVersionDisplay.Visibility = Visibility.Collapsed; return; }
        try
        {
            var psi = new ProcessStartInfo(gitPath) { Arguments = "--version", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            using var process = Process.Start(psi);
            if (process == null) { GitVersionDisplay.Visibility = Visibility.Collapsed; return; }
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                string version = output.Trim();
                const string prefix = "git version ";
                if (version.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) version = version.Substring(prefix.Length);
                GitVersionDisplay.Text = string.Format(App.Localizer.GetLocalizedString("SettingsGitVersion"), version);
                GitVersionDisplay.Visibility = Visibility.Visible;
            }
            else GitVersionDisplay.Visibility = Visibility.Collapsed;
        }
        catch { GitVersionDisplay.Visibility = Visibility.Collapsed; }
    }
}