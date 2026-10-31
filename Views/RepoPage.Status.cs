using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentGit.Services;

namespace FluentGit.Views;

public sealed partial class RepoPage
{
    // ========== 刷新变更状态 ==========
    private async void RefreshChangeStatus()
    {
        var repoPath = AppState.CurrentRepoPath;
        if (string.IsNullOrEmpty(repoPath) || !Directory.Exists(repoPath))
        {
            ChangeStatusText.Text = App.Localizer.GetLocalizedString("RepoStatusUnopened");
            return;
        }

        var settings = SettingsService.Load();
        string? gitPath = settings.GitPath;
        if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath))
        {
            ChangeStatusText.Text = App.Localizer.GetLocalizedString("RepoErrGitPath");
            return;
        }

        try
        {
            ChangeStatusText.Text = App.Localizer.GetLocalizedString("RepoStatusReading");

            var summary = await Task.Run(() => GitService.GetChangeSummary(gitPath, repoPath));

            if (summary.Modified == 0 && summary.Added == 0 && summary.Untracked == 0)
            {
                ChangeStatusText.Text = App.Localizer.GetLocalizedString("RepoClean");
            }
            else
            {
                var parts = new List<string>();
                if (summary.Modified > 0) parts.Add(string.Format(App.Localizer.GetLocalizedString("RepoMod"), summary.Modified));
                if (summary.Added > 0) parts.Add(string.Format(App.Localizer.GetLocalizedString("RepoAdd"), summary.Added));
                if (summary.Untracked > 0) parts.Add(string.Format(App.Localizer.GetLocalizedString("RepoUntracked"), summary.Untracked));
                ChangeStatusText.Text = string.Join("  ·  ", parts);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"RefreshChangeStatus 异常: {ex.Message}");
            ChangeStatusText.Text = App.Localizer.GetLocalizedString("RepoStatusReadFailed");
        }
    }

    // ========== 更新界面状态 ==========
    private void UpdateCloneUI()
    {
        var repoPath = AppState.CurrentRepoPath;
        bool isRepoOpen = !string.IsNullOrEmpty(repoPath) && Directory.Exists(repoPath);
        if (isRepoOpen)
        {
            RepoOpsPanel.Visibility = Visibility.Visible;
            CommitPanel.Visibility = Visibility.Visible;
            RefreshChangeStatus();
        }
        else
        {
            RepoOpsPanel.Visibility = Visibility.Collapsed;
            CommitPanel.Visibility = Visibility.Collapsed;
        }
    }
}