﻿using MessageBox.Avalonia;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;

namespace CosmoInstaller.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
  private string _titleText = "Welcome to the Cosmo installer!";
  public string TitleText
  {
    get => _titleText;
    set => this.RaiseAndSetIfChanged(ref _titleText, value);
  }

  private bool _isNotInstalling = true;
  public bool IsNotInstalling
  {
    get => _isNotInstalling;
    set => this.RaiseAndSetIfChanged(ref _isNotInstalling, value);
  }

  private bool _selectDirectoryButtonEnabled = true;
  public bool SelectDirectoryButtonEnabled
  {
    get => _selectDirectoryButtonEnabled;
    set => this.RaiseAndSetIfChanged(ref _selectDirectoryButtonEnabled, value);
  }

  private string _selectedDirectory;
  public string SelectedDirectory
  {
    get => _selectedDirectory;
    set => this.RaiseAndSetIfChanged(ref _selectedDirectory, value);
  }

  private bool _progressBarVisible;
  public bool ProgressBarVisible
  {
    get => _progressBarVisible;
    set => this.RaiseAndSetIfChanged(ref _progressBarVisible, value);
  }

  private int _progressBarValue;
  public int ProgressBarValue
  {
    get => _progressBarValue;
    set => this.RaiseAndSetIfChanged(ref _progressBarValue, value);
  }

  private bool _finishedCloseVisible = false;
  public bool FinishedCloseVisible
  {
    get => _finishedCloseVisible;
    set => this.RaiseAndSetIfChanged(ref _finishedCloseVisible, value);
  }

  public ReactiveCommand<Unit, Unit> InstallCommand { get; }
  public ReactiveCommand<Unit, Unit> SuccessfulExitCommand { get; }

  private bool _errored = false;

  public MainWindowViewModel()
  {
    _selectedDirectory = GetDefaultInstallationDirectory();
    InstallCommand = ReactiveCommand.Create(InstallCosmo);
    SuccessfulExitCommand = ReactiveCommand.Create(SuccessfulExit);
    Console.WriteLine("Initialized app.");
  }

  private string GetDefaultInstallationDirectory()
  {
    string defaultDirectory = string.Empty;
    if (OperatingSystem.IsWindows())
      defaultDirectory = "C:\\Program Files";
    else if (OperatingSystem.IsLinux())
      defaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    else if (OperatingSystem.IsMacOS())
      defaultDirectory = "/Applications";

    return defaultDirectory;
  }

  private async void InstallCosmo()
  {
    ProgressBarVisible = true;
    IsNotInstalling = false;
    TitleText = "Installing...";

    string fullSelectedDir = Path.GetFullPath(_selectedDirectory);
    string absolutePath = Path.GetFullPath(Path.Combine(fullSelectedDir, (OperatingSystem.IsWindows() ? "" : ".") + "cosmo"));
    await Task.Run(() => Installation.InstallCosmo(
      UpdateProgress,
      UpdateTitle,
      MarkErrored,
      absolutePath
    ));

    ProgressBarVisible = false;
    if (_errored) return;

    FinishedCloseVisible = true;
  }

  private void SuccessfulExit()
    => Environment.Exit(0);

  private void MarkErrored()
    => _errored = true;

  private void UpdateTitle(string title)
    => TitleText = title;

  private void UpdateProgress(int progress)
    => ProgressBarValue = progress;
}