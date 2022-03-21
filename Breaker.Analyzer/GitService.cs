using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Breaker.Analyzer;

public static class GitService
{
    private static bool TryGetGitDirectory(DirectoryInfo directory, out DirectoryInfo gitDirectoryInfo)
    {
        var gitPath = Path.Combine(directory.FullName, ".git");

        while (!Directory.Exists(gitPath))
        {
            if (directory.Parent is null)
            {
                gitDirectoryInfo = null;
                return false;
            }

            gitPath = Path.Combine(directory.Parent.FullName, ".git");
        }

        gitDirectoryInfo = new DirectoryInfo(gitPath);
        return true;
    }

    public static DirectoryInfo GetOrUpdateSolution(DirectoryInfo solutionDirectoryInfo, string gitRef = null)
    {
        var clonePath = Path.Combine(solutionDirectoryInfo.FullName, ".breaker");
        if (!Directory.Exists(clonePath)) Directory.CreateDirectory(clonePath);

        if (!TryGetGitDirectory(solutionDirectoryInfo, out var gitDirectoryInfo)) return null;
        var gitConfigInfo = gitDirectoryInfo.GetFiles("config", SearchOption.TopDirectoryOnly).Single();
        var gitHeadInfo = gitDirectoryInfo.GetFiles("HEAD", SearchOption.TopDirectoryOnly).Single();

        var remoteUrl = File.ReadAllLines(gitConfigInfo.FullName).Single(l => l.Trim().StartsWith("url"))
            .Split('=')[1].Trim();

        var repoName = remoteUrl.Split('/').Last().Replace(".git", "");

        var clonedRepoPath = Path.Combine(clonePath, repoName);

        var gitHeadText = File.ReadAllText(gitHeadInfo.FullName).Trim();

        gitRef ??= gitHeadText.StartsWith("ref: refs/heads/")
            ? gitHeadText.Split("ref: refs/heads/").Last()
            : gitHeadText;

        if (Directory.Exists(clonedRepoPath))
        {
            if (!TryFetch(clonedRepoPath, out var pullError)) throw new Exception(pullError);
        }
        else
        {
            if (!TryClone(remoteUrl, clonePath, out var cloneError)) throw new Exception(cloneError);
        }

        if (!TryCheckout(gitRef, clonedRepoPath, out var checkoutError)) throw new Exception(checkoutError);
        TryPull(clonedRepoPath, out _);

        var result = new DirectoryInfo(clonedRepoPath);

        return result.Exists ? result : throw new Exception("cloned repo path does not exist");
    }

    private static bool TryFetch(string clonedRepoPath, out string error) => TryExecuteGitCommand("fetch", clonedRepoPath, out error);

    private static bool TryClone(string remoteUrl, string clonePath, out string error)
        => TryExecuteGitCommand($"clone {remoteUrl}", clonePath, out error);

    private static bool TryCheckout(string gitRef, string clonedRepoPath, out string error)
        => TryExecuteGitCommand($"checkout {gitRef}", clonedRepoPath, out error);

    private static bool TryPull(string clonedRepoPath, out string error) => TryExecuteGitCommand("pull", clonedRepoPath, out error);

    private static bool TryExecuteGitCommand(string gitCommand, string workingDirectory, out string error)
    {
        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = gitCommand,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            }
        };

        proc.Start();
        error = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return proc.ExitCode == 0;
    }
}