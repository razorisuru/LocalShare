using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;
using LocalShare.Common;

namespace LocalShare.App.ViewModels;

public partial class ProfileSettingsViewModel : ObservableObject
{
    private readonly Profile _localProfile;
    private readonly IProfileRepository _profileRepo;
    private readonly IUpdateService _updateService;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _accentColor = "#7C5CFC";

    [ObservableProperty]
    private string _publicSpacePath = string.Empty;

    [ObservableProperty]
    private string _receivedFilesRoot = string.Empty;

    [ObservableProperty]
    private bool _enableNotifications = true;

    [ObservableProperty]
    private string _deviceId = string.Empty;

    [ObservableProperty]
    private string _localIp = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Software Update Properties
    [ObservableProperty]
    private string _currentVersion = "v1.0.0";

    [ObservableProperty]
    private string _developerName = "Isuru Bandara";

    [ObservableProperty]
    private string _developerWebsite = "https://razorisuru.com";

    [ObservableProperty]
    private string _updateManifestUrl = "https://raw.githubusercontent.com/razorisuru/LocalShare/main/dist/latest_version.json";

    [ObservableProperty]
    private string _updateStatusMessage = "Check for available software updates on LAN / Web.";

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isDownloadingUpdate;

    [ObservableProperty]
    private double _updateProgressPercentage;

    [ObservableProperty]
    private UpdateInfo? _availableUpdateInfo;

    public List<string> ColorPresets { get; } = new()
    {
        "#7C5CFC", // Studio Purple
        "#8B70FF", // Studio Bright
        "#10B981", // Emerald Green
        "#8B5CF6", // Cyber Purple
        "#FB27F5", // Neon Pink
        "#F97316", // Sunset Orange
        "#EF4444", // Crimson Red
        "#06B6D4"  // Ocean Cyan
    };

    public ProfileSettingsViewModel(Profile localProfile, IProfileRepository profileRepo, IUpdateService updateService)
    {
        _localProfile = localProfile;
        _profileRepo = profileRepo;
        _updateService = updateService;

        DisplayName = _localProfile.DisplayName;
        AccentColor = string.IsNullOrWhiteSpace(_localProfile.AccentColor) ? "#7C5CFC" : _localProfile.AccentColor;
        PublicSpacePath = _localProfile.PublicSpacePath ?? string.Empty;
        ReceivedFilesRoot = _localProfile.ReceivedFilesRoot;
        EnableNotifications = _localProfile.EnableNotifications;
        DeviceId = _localProfile.DeviceId;
        LocalIp = NetworkHelpers.GetLocalIpAddress();
        CurrentVersion = $"v{_updateService.CurrentVersion}";
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        IsUpdateAvailable = false;
        UpdateStatusMessage = "Checking update manifest for new releases...";

        try
        {
            var result = await _updateService.CheckForUpdatesAsync(UpdateManifestUrl);
            if (result.IsSuccess)
            {
                if (result.Value != null)
                {
                    AvailableUpdateInfo = result.Value;
                    IsUpdateAvailable = true;
                    UpdateStatusMessage = $"🚀 New version v{result.Value.Version} is available! (Released: {result.Value.ReleaseDate})";
                }
                else
                {
                    AvailableUpdateInfo = null;
                    IsUpdateAvailable = false;
                    UpdateStatusMessage = $"✅ You are running the latest version of LocalShare ({CurrentVersion}).";
                }
            }
            else
            {
                UpdateStatusMessage = $"⚠️ Update check failed: {result.Error}";
            }
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    private async Task ApplyUpdateAsync()
    {
        if (AvailableUpdateInfo == null || string.IsNullOrWhiteSpace(AvailableUpdateInfo.DownloadUrl))
        {
            UpdateStatusMessage = "No download URL available for update.";
            return;
        }

        IsDownloadingUpdate = true;
        UpdateStatusMessage = $"Downloading LocalShare v{AvailableUpdateInfo.Version} installer...";
        UpdateProgressPercentage = 0;

        var result = await _updateService.DownloadAndApplyUpdateAsync(AvailableUpdateInfo, progress =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                UpdateProgressPercentage = progress;
                UpdateStatusMessage = $"Downloading update installer: {progress:F0}%";
            });
        });

        if (!result.IsSuccess)
        {
            IsDownloadingUpdate = false;
            UpdateStatusMessage = $"❌ Update failed: {result.Error}";
        }
    }

    [RelayCommand]
    private void SelectPresetColor(string? colorHex)
    {
        if (!string.IsNullOrWhiteSpace(colorHex))
        {
            AccentColor = colorHex;
            StatusMessage = $"Selected color preset: {colorHex}";
        }
    }

    [RelayCommand]
    private void BrowsePublicSpaceFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select a folder to share as your Public Space on LAN"
        };

        if (dialog.ShowDialog() == true)
        {
            PublicSpacePath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseReceivedFilesFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select root folder for received LAN files"
        };

        if (dialog.ShowDialog() == true)
        {
            ReceivedFilesRoot = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void OpenFolderInExplorer(string? folderPath)
    {
        if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
        {
            Process.Start("explorer.exe", folderPath);
        }
        else
        {
            StatusMessage = "Folder path does not exist yet.";
        }
    }

    [RelayCommand]
    private void CopyDeviceIdToClipboard()
    {
        if (!string.IsNullOrWhiteSpace(DeviceId))
        {
            Clipboard.SetText(DeviceId);
            StatusMessage = "Device ID copied to clipboard!";
        }
    }

    [RelayCommand]
    private void OpenDeveloperWebsite()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DeveloperWebsite,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open browser: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        _localProfile.DisplayName = DisplayName;
        _localProfile.AccentColor = AccentColor;
        _localProfile.PublicSpacePath = PublicSpacePath;
        _localProfile.ReceivedFilesRoot = ReceivedFilesRoot;
        _localProfile.EnableNotifications = EnableNotifications;

        await _profileRepo.SaveProfileAsync(_localProfile);
        StatusMessage = "✅ Profile & Storage settings saved successfully!";
    }
}
