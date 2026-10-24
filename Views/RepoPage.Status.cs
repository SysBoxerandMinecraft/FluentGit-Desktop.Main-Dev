using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentGit.Services;

namespace FluentGit.Views;

public sealed partial class RepoPage
{
    private async void RefreshChangeStatus()
    {
        var repoPath = AppState.CurrentRepoPath;
        if (string.IsNullOrEmpty(repoPath) || !Directory.Exists(repoPath))
        {
            ChangeStatusText.Text = Loc.GetString("RepoStatusUnopened");
            return;
        }

        var settings = SettingsService.Load();
        string? gitPath = settings.GitPath;
        if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath))
        {
            ChangeStatusText.Text = Loc.GetString("RepoErrGitPath");
            return;
        }

        try
        {
            ChangeStatusText.Text = Loc.GetString("RepoStatusReading");

            var summary = await Task.Run(() => GitService.GetChangeSummary(gitPath, repoPath));

            if (summary.Modified == 0 && summary.Added == 0 && summary.Untracked == 0)
            {
                ChangeStatusText.Text = Loc.GetString("RepoClean");
            }
            else
            {
                var parts = new List<string>();
                if (summary.Modified > 0) parts.Add(string.Format(Loc.GetString("RepoMod"), summary.Modified));
                if (summary.Added > 0) parts.Add(string.Format(Loc.GetString("RepoAdd"), summary.Added));
                if (summary.Untracked > 0) parts.Add(string.Format(Loc.GetString("RepoUntracked"), summary.Untracked));
                ChangeStatusText.Text = string.Join("  ·  ", parts);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"RefreshChangeStatus 异常: {ex.Message}");
            ChangeStatusText.Text = Loc.GetString("RepoStatusReadFailed");
        }
    }

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