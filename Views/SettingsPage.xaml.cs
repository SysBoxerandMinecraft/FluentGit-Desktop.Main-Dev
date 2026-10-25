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
        _settings.Add(new SearchEntry { Title = Loc.GetString("SettingsSearchTitleLanguage"), Keywords = new[] { "语言", "language", "i18n" }, Item = ItemLanguage, Group = GroupLanguage });
        _settings.Add(new SearchEntry { Title = Loc.GetString("SettingsSearchTitleGit"), Keywords = new[] { "git", "path", "install" }, Item = ItemGitPath, Group = GroupGit });
        _settings.Add(new SearchEntry { Title = Loc.GetString("SettingsSearchTitleBackdrop"), Keywords = new[] { "背景", "效果", "mica", "acrylic" }, Item = ItemBackdrop, Group = GroupAppearance });
        _settings.Add(new SearchEntry { Title = Loc.GetString("SettingsSearchTitleFilter"), Keywords = new[] { "过滤", "编译", "bin", "obj" }, Item = ItemFilterArtifacts, Group = GroupTreeView });
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

        var dialog = new ContentDialog
        {
            Title = Loc.GetString("CommonInfo"),
            Content = Loc.GetString("SettingsLanguageDesc.Text"), // 这里已去掉 .Text
            CloseButtonText = Loc.GetString("CommonOK"),
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void OnFilterArtifactsChecked(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        var settings = SettingsService.Load(); settings.FilterBuildArtifacts = true; SettingsService.Save(settings);
        Toast.Success(Loc.GetString("SettingsToastFilterOn"), Loc.GetString("SettingsToastSettingsUpdated"));
    }

    private void OnFilterArtifactsUnchecked(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        var settings = SettingsService.Load(); settings.FilterBuildArtifacts = false; SettingsService.Save(settings);
        Toast.Info(Loc.GetString("SettingsToastFilterOff"), Loc.GetString("SettingsToastSettingsUpdated"));
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
            "Default" => Loc.GetString("SettingsBackdropDefault"),
            "Mica"    => Loc.GetString("SettingsBackdropMica"),
            "Acrylic" => Loc.GetString("SettingsBackdropAcrylic"),
            "None"    => Loc.GetString("SettingsBackdropNone"),
            _         => tag
        };
        Toast.Success(string.Format(Loc.GetString("SettingsToastBackdropChanged"), displayName), Loc.GetString("SettingsToastAppearanceUpdated"));
    }

    private void UpdateBackdropHint(string tag)
    {
        string hint = tag switch
        {
            "Default" => MicaController.IsSupported() ? Loc.GetString("SettingsHintDefaultOk") : Loc.GetString("SettingsHintDefaultFail"),
            "Mica" => MicaController.IsSupported() ? Loc.GetString("SettingsHintMicaOk") : Loc.GetString("SettingsHintMicaFail"),
            "Acrylic" => DesktopAcrylicController.IsSupported() ? Loc.GetString("SettingsHintAcrylicOk") : Loc.GetString("SettingsHintAcrylicFail"),
            "None" => Loc.GetString("SettingsHintNone"),
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
                Toast.Warning(Loc.GetString("SettingsToastGitNotFound"));
                return;
            }
            if (!GitPathHelper.ValidateGitPath(gitPath))
            {
                UpdateSelectedPathDisplay(gitPath);
                await UpdateGitVersionAsync(gitPath);
                Toast.Error(Loc.GetString("SettingsToastGitInvalid"));
                return;
            }
            
            // 【关键修复】重新加载一次最新的 settings，防止覆盖用户刚切换的语言
            var latestSettings = SettingsService.Load(); 
            latestSettings.GitPath = gitPath;
            SettingsService.Save(latestSettings);

            UpdateSelectedPathDisplay(gitPath);
            await UpdateGitVersionAsync(gitPath);
            Toast.Success(string.Format(Loc.GetString("SettingsToastGitFound"), gitPath));
        }
        catch (Exception ex) { Toast.Error(string.Format(Loc.GetString("SettingsToastSearchError"), ex.Message)); }
        finally { LoadingRing.IsActive = false; }
    }

    private void UpdateSelectedPathDisplay(string path)
    {
        SelectedPathDisplay.Text = string.IsNullOrEmpty(path) ? Loc.GetString("SettingsGitNoPath") : string.Format(Loc.GetString("SettingsGitCurrentPath"), path);
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
                GitVersionDisplay.Text = string.Format(Loc.GetString("SettingsGitVersion"), version);
                GitVersionDisplay.Visibility = Visibility.Visible;
            }
            else GitVersionDisplay.Visibility = Visibility.Collapsed;
        }
        catch { GitVersionDisplay.Visibility = Visibility.Collapsed; }
    }
}