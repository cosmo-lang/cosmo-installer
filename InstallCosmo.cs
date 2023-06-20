using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using Avalonia.Threading;
using MessageBox.Avalonia;

namespace CosmoInstaller;

public static class Installation
{
    private const float Step = (1f / 13f) * 100;
    private static float _progress;
    private static bool _errored;
    private static bool _finished;

    public static void InstallCosmo(
        Action<int> updateProgress,
        Action<string> updateTitle,
        Action markErrored,
        Action markFinished,
        string path,
        string installerPath)
    {
        _progress = 0;
        _errored = false;
        _finished = false;

        Action<int>? _updateProgress = updateProgress;
        Action<string>? _updateTitle = updateTitle;
        Action? _markErrored = markErrored;
        Action? _markFinished = markFinished;

        if (OperatingSystem.IsWindows() && !IsAdmin())
        {
            ShowErrorMessageBox("Cannot install. You are not running with elevated privileges.\nRestart the app as an administrator and try again.");
            return;
        }

        Log("Creating installation environment...");

        if (Directory.Exists(path))
        {
            Log("Installation directory exists, skipping creation...");
        }
        else
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception err)
            {
                ShowErrorMessageBox($"Failed to create directory (run as administrator?): {err.Message}");
                return;
            }
        }

        Log("Changing environment directory...");
        StepProgress();

        try
        {
            Environment.CurrentDirectory = path;
        }
        catch (Exception err)
        {
            ShowErrorMessageBox($"Failed to change directory (run as administrator?): {err.Message}");
            return;
        }

        StepProgress();
        Log("Pulling repository...");
        string[] dirEntries;

        try
        {
            dirEntries = Directory.GetFileSystemEntries(".");
            if (dirEntries.Length != 0)
            {
                if (OperatingSystem.IsWindows())
                {
                    ExecuteCommand("Failed to remove shard.lock", "powershell", "-c \"rm -Force ./shard.lock\"");
                }
                else
                {
                    ExecuteCommand("Failed to remove shard.lock", "rm", "-f shard.lock");
                }

                ExecuteGitCommand("pull origin master --allow-unrelated-histories", "Failed to pull from the repository (is git installed?)");
            }
            else
            {
                ExecuteGitCommand("clone https://github.com/cosmo-lang/cosmo.git .", "Failed to clone the repository (is git installed?)");
            }
        }
        catch (Exception err)
        {
            ShowErrorMessageBox($"Failed to read the directory (run as administrator?): {err.Message}");
            return;
        }

        StepProgress();
        Log("Fetching tags...");
        ExecuteGitCommand("fetch --tags", "Failed to fetch release tags");
        StepProgress();

        Log("Fetching latest release...");
        string latestTag = ExecuteGitCommand("describe --tags --abbrev=0", "Failed to get the latest release tag");
        StepProgress();

        Log("Checking out latest release...");
        ExecuteGitCommand($"checkout {latestTag}", "Failed to checkout the latest release");
        StepProgress();

        Log("Checking for Crystal installation...");
        ProcessResult? crystalCheckOutput = null;

        try
        {
            crystalCheckOutput = ExecuteCommand(null, "crystal", "-v");
        }
        catch (Win32Exception)
        {
          // The 'crystal' command was not found in the PATH.
        }

        if (crystalCheckOutput == null || crystalCheckOutput.ExitCode != 0)
        {
            Log("Installing Crystal...");

            if (OperatingSystem.IsWindows())
            {
                Log("Checking for Scoop installation...");
                ExecuteCommand("Failed to install Crystal: \nScoop is not installed, but is required to install Crystal for Windows. \nIf you don't want to use Scoop, please manually install Crystal.", "powershell.exe", "-c \"scoop\"");

                Log("Found Scoop!");
                Log("Adding Crystal bucket...");
                ExecuteCommand("Failed to add Crystal bucket", "scoop", "bucket add crystal-preview https://github.com/neatorobito/scoop-crystal");
                StepProgress();

                ProcessResult crtCheckOutput = ExecuteCommand("Failed to execute 'where'", "where", "cl.exe");
                if (crtCheck            Output.Contains("Visual Studio"))
            {
                ShowErrorMessageBox("Failed to install Crystal: Visual Studio is currently installed on your system. \nPlease uninstall Visual Studio before installing Crystal.");
                return;
            }

            Log("Installing Crystal via Scoop...");

            try
            {
                ExecuteCommand("Failed to install Crystal", "scoop", "install crystal-preview");
            }
            catch (Exception err)
            {
                ShowErrorMessageBox($"Failed to install Crystal via Scoop: {err.Message}");
                return;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            ExecuteCommand("Failed to install dependencies", "sudo", "apt-get update && sudo apt-get install -y curl libgc-dev libevent-dev libssl-dev libxml2-dev libyaml-dev zlib1g-dev");
            StepProgress();

            ExecuteCommand("Failed to add key for Crystal's package manager", "curl", "-sSL https://keybase.io/crystal/pgp_keys.asc | sudo apt-key add -");
            StepProgress();

            ExecuteCommand("Failed to add APT repository for Crystal", "echo", "\"deb https://dist.crystal-lang.org/apt crystal main\" | sudo tee /etc/apt/sources.list.d/crystal.list");
            StepProgress();

            ExecuteCommand("Failed to install Crystal", "sudo", "apt-get update && sudo apt-get install crystal");
        }
    }
    else
    {
        Log("Crystal already installed!");
    }

    StepProgress();
    Log("Building Cosmo...");
    ExecuteCommand("Failed to build Cosmo", "shards", "install --production && shards build");
    StepProgress();

    Log("Done! 🎉");
    _finished = true;

    void StepProgress()
    {
        _progress += Step;
        _updateProgress?.Invoke((int)_progress);
    }

    void Log(string message)
    {
        _updateTitle?.Invoke(message);
    }

    void ShowErrorMessageBox(string message)
    {
        _errored = true;
        _markErrored?.Invoke();
        Dispatcher.UIThread.InvokeAsync(() =>
            MessageBoxManager.GetMessageBoxStandardWindow("Error", message).Show());
    }
}

private static bool IsAdmin()
{
    WindowsIdentity identity = WindowsIdentity.GetCurrent();
    WindowsPrincipal principal = new WindowsPrincipal(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
}

private static ProcessResult ExecuteCommand(string? errorMessage, string command, string arguments)
{
    var processInfo = new ProcessStartInfo
    {
        FileName = command,
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = new Process { StartInfo = processInfo };
    process.Start();
    var outputBuilder = new StringBuilder();
    var errorBuilder = new StringBuilder();

    process.OutputDataReceived += (sender, e) =>
    {
        if (!string.IsNullOrEmpty(e.Data))
            outputBuilder.AppendLine(e.Data);
    };

    process.ErrorDataReceived += (sender, e) =>
    {
        if (!string.IsNullOrEmpty(e.Data))
            errorBuilder.AppendLine(e.Data);
    };

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new Exception(errorMessage ?? $"Command '{command} {arguments}' exited with code {process.ExitCode}\n{errorBuilder}");
    }

    return new ProcessResult(outputBuilder.ToString(), errorBuilder.ToString(), process.ExitCode);
}

private static string ExecuteGitCommand(string arguments, string errorMessage)
{
    ProcessResult result = ExecuteCommand(errorMessage, "git", arguments);
    return result.Output.Trim();
}

private class ProcessResult
{
    public string Output { get; }
    public string Error { get; }
    public int ExitCode { get; }

    public ProcessResult(string output, string error, int exitCode)
    {
        Output = output;
        Error = error;
        ExitCode = exitCode;
    }
}
}
