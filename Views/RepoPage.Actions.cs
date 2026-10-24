using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using FluentGit.Services;

namespace FluentGit.Views;

public sealed partial class RepoPage
{
    private async Task ExecuteGitOperationAsync(
        string operationName,
        Func<string, string, Task<(bool Success, string Message)>> action,
        Action? onSuccess = null,
        string? startMessage = null)
    {
        if (_isBusy) return;

        var repoPath = AppState.CurrentRepoPath;
        if (string.IsNullOrEmpty(repoPath) || !Directory.Exists(repoPath))
        {
            ShowInfoBar(Loc.GetString("CommonError"), Loc.GetString("RepoErrNoRepo"), InfoBarSeverity.Error);
            return;
        }

        var settings = SettingsService.Load();
        string? gitPath = settings.GitPath;
        if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath))
        {
            ShowInfoBar(Loc.GetString("CommonError"), Loc.GetString("RepoErrGitPath"), InfoBarSeverity.Error);
            return;
        }

        try
        {
            _isBusy = true;

            if (!string.IsNullOrEmpty(startMessage))
                ShowInfoBar(Loc.GetString("CommonInfo"), startMessage, InfoBarSeverity.Informational);

            var result = await action(gitPath, repoPath);

            if (result.Success)
            {
                ShowInfoBar(Loc.GetString("CommonSuccess"), result.Message, InfoBarSeverity.Success);
                onSuccess?.Invoke();
            }
            else
            {
                ShowInfoBar(Loc.GetString("CommonError"), result.Message, InfoBarSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"{operationName} 异常: {ex.Message}");
            ShowInfoBar(Loc.GetString("CommonError"), string.Format(Loc.GetString("RepoErrActionFailed"), ex.Message), InfoBarSeverity.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void OnInitRepository(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        string path = RepoPathTextBox.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            ShowInfoBar(Loc.GetString("CommonWarning"), Loc.GetString("RepoErrInitPath"), InfoBarSeverity.Warning);
            return;
        }
        if (!Directory.Exists(path))
        {
            ShowInfoBar(Loc.GetString("CommonError"), Loc.GetString("RepoErrInitNotExist"), InfoBarSeverity.Error);
            return;
        }
        if (GitService.IsGitRepository(path))
        {
            ShowInfoBar(Loc.GetString("CommonInfo"), Loc.GetString("RepoErrInitAlready"), InfoBarSeverity.Informational);
            return;
        }

        var settings = SettingsService.Load();
        string? gitPath = settings.GitPath;
        if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath))
        {
            ShowInfoBar(Loc.GetString("CommonError"), Loc.GetString("RepoErrGitPath"), InfoBarSeverity.Error);
            return;
        }

        try
        {
            _isBusy = true;
            bool success = GitService.InitRepository(gitPath, path);
            if (success)
            {
                AppLogger.OK(TAG, $"仓库初始化成功: {path}");
                ShowInfoBar(Loc.GetString("CommonSuccess"), string.Format(Loc.GetString("RepoInitSuccess"), path), InfoBarSeverity.Success);
            }
            else
            {
                ShowInfoBar(Loc.GetString("CommonError"), Loc.GetString("RepoInitFailed"), InfoBarSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"git init 异常: {ex.Message}");
            ShowInfoBar(Loc.GetString("CommonError"), ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async void OnOpenRepo(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        try
        {
            _isBusy = true;

            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            folderPicker.FileTypeFilter.Add("*");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null) return;

            string repoPath = folder.Path;
            if (GitService.IsGitRepository(repoPath))
            {
                AppState.SetRepository(repoPath);
                RepoActionsPanel.Visibility = Visibility.Collapsed;
                RepoNameDisplay.Text = Path.GetFileName(repoPath);
                RepoNameDisplay.Visibility = Visibility.Visible;
                UpdateCloneUI();
                AppLogger.OK(TAG, $"已打开仓库: {repoPath}");
                ShowInfoBar(Loc.GetString("CommonSuccess"), string.Format(Loc.GetString("RepoOpenSuccess"), Path.GetFileName(repoPath)), InfoBarSeverity.Success);
            }
            else
            {
                ShowInfoBar(Loc.GetString("CommonError"), Loc.GetString("RepoOpenNotGit"), InfoBarSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(TAG, $"打开仓库异常: {ex.Message}");
            ShowInfoBar(Loc.GetString("CommonError"), ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async void OnPullRepository(object sender, RoutedEventArgs e)
    {
        await ExecuteGitOperationAsync(
            "OnPullRepository",
            async (git, repo) =>
            {
                var r = await Task.Run(() => GitService.PullRepository(git, repo));
                return (r.Success, r.Message);
            },
            startMessage: Loc.GetString("RepoStatusPulling"));
    }

    private async void OnFetchRepository(object sender, RoutedEventArgs e)
    {
        await ExecuteGitOperationAsync(
            "OnFetchRepository",
            async (git, repo) =>
            {
                var r = await Task.Run(() => GitService.FetchRepository(git, repo));
                return (r.Success, r.Message);
            },
            startMessage: Loc.GetString("RepoStatusFetching"));
    }

    private async void OnAddAll(object sender, RoutedEventArgs e)
    {
        await ExecuteGitOperationAsync(
            "OnAddAll",
            async (git, repo) =>
            {
                var r = await Task.Run(() => GitService.AddAll(git, repo));
                return (r.Success, r.Message);
            },
            onSuccess: RefreshChangeStatus);
    }

    private async void OnCommit(object sender, RoutedEventArgs e)
    {
        string message = CommitMessageTextBox.Text.Trim();
        if (string.IsNullOrEmpty(message))
        {
            ShowInfoBar(Loc.GetString("CommonWarning"), Loc.GetString("RepoErrNoCommitMsg"), InfoBarSeverity.Warning);
            return;
        }

        await ExecuteGitOperationAsync(
            "OnCommit",
            async (git, repo) =>
            {
                var r = await Task.Run(() => GitService.Commit(git, repo, message));
                return (r.Success, r.Message);
            },
            onSuccess: () =>
            {
                CommitMessageTextBox.Text = "";
                RefreshChangeStatus();
            });
    }

    private void OnRefreshChangeStatus(object sender, RoutedEventArgs e)
    {
        RefreshChangeStatus();
    }

    private async void OnPushRepository(object sender, RoutedEventArgs e)
    {
        await ExecuteGitOperationAsync(
            "OnPushRepository",
            async (git, repo) =>
            {
                var r = await Task.Run(() => GitService.PushRepository(git, repo));
                return (r.Success, r.Message);
            },
            startMessage: Loc.GetString("RepoStatusPushing"));
    }
}