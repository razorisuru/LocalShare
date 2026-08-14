using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;
using LocalShare.Common;

namespace LocalShare.App.ViewModels;

public class PublicFileItemViewModel
{
    public PublicShareEntry Entry { get; }
    public string FileName => Entry.FileName;
    public string RelativePath => Entry.RelativePath;
    public long SizeBytes => Entry.SizeBytes;

    public string FormattedSize
    {
        get
        {
            double bytes = Entry.SizeBytes;
            if (bytes >= 1024 * 1024 * 1024)
                return $"{bytes / (1024 * 1024 * 1024):0.00} GB";
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024 * 1024):0.00} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024:0.00} KB";
            return $"{bytes} Bytes";
        }
    }

    public string FileIcon
    {
        get
        {
            var ext = Path.GetExtension(FileName).ToLowerInvariant();
            return ext switch
            {
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" => "🖼️",
                ".mp4" or ".mkv" or ".avi" or ".mov" => "🎥",
                ".mp3" or ".wav" or ".flac" or ".aac" => "🎵",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
                ".pdf" or ".doc" or ".docx" or ".txt" => "📄",
                ".exe" or ".msi" or ".dll" => "⚙️",
                _ => "📁"
            };
        }
    }

    public PublicFileItemViewModel(PublicShareEntry entry)
    {
        Entry = entry;
    }
}

public partial class PublicSpaceViewModel : ObservableObject
{
    private readonly IPublicSpaceService _publicSpaceService;
    private readonly IDiscoveryService _discoveryService;
    private readonly Profile _localProfile;

    [ObservableProperty]
    private ObservableCollection<Peer> _peers = new();

    [ObservableProperty]
    private Peer? _selectedPeer;

    [ObservableProperty]
    private ObservableCollection<PublicFileItemViewModel> _publicFiles = new();

    [ObservableProperty]
    private string _statusMessage = "Select a LAN peer to browse their shared Public Space files.";

    public PublicSpaceViewModel(IPublicSpaceService publicSpaceService, IDiscoveryService discoveryService, Profile localProfile)
    {
        _publicSpaceService = publicSpaceService;
        _discoveryService = discoveryService;
        _localProfile = localProfile;

        RefreshPeers();
    }

    [RelayCommand]
    public void RefreshPeers()
    {
        Peers.Clear();
        foreach (var peer in _discoveryService.GetDiscoveredPeers().Where(p => p.HasPublicSpace))
        {
            Peers.Add(peer);
        }

        if (Peers.Count > 0 && SelectedPeer == null)
        {
            SelectedPeer = Peers.First();
        }
        else if (Peers.Count == 0)
        {
            StatusMessage = "No LAN peers currently sharing a Public Space.";
        }
    }

    public void SelectPeerAndLoadPublicSpace(Peer peer)
    {
        RefreshPeers();
        var match = Peers.FirstOrDefault(p => p.DeviceId == peer.DeviceId);
        if (match == null)
        {
            Peers.Add(peer);
            match = peer;
        }
        SelectedPeer = match;
    }

    partial void OnSelectedPeerChanged(Peer? value)
    {
        if (value != null)
        {
            _ = LoadRemoteFilesAsync(value);
        }
        else
        {
            PublicFiles.Clear();
        }
    }

    private async Task LoadRemoteFilesAsync(Peer peer)
    {
        StatusMessage = $"Loading shared files from {peer.DisplayName}...";
        var res = await _publicSpaceService.FetchRemotePublicFilesAsync(peer);
        if (res.IsSuccess && res.Value != null)
        {
            PublicFiles.Clear();
            foreach (var file in res.Value)
            {
                PublicFiles.Add(new PublicFileItemViewModel(file));
            }
            StatusMessage = $"Found {PublicFiles.Count} public file(s) shared by {peer.DisplayName}.";
        }
        else
        {
            StatusMessage = $"Error loading files: {res.Error}";
        }
    }

    [RelayCommand]
    private async Task DownloadFileAsync(PublicFileItemViewModel? item)
    {
        if (item == null || SelectedPeer == null) return;

        // Clean peer display name for folder creation (e.g. Received/PRAMUKA-PC/)
        string cleanPeerName = string.Concat(SelectedPeer.DisplayName.Split(Path.GetInvalidFileNameChars())).Trim();
        if (string.IsNullOrWhiteSpace(cleanPeerName)) cleanPeerName = SelectedPeer.IpAddress.Replace('.', '_');

        var rootReceivedFolder = _localProfile.ReceivedFilesRoot;
        if (string.IsNullOrWhiteSpace(rootReceivedFolder))
        {
            rootReceivedFolder = LocalShare.Common.Constants.ReceivedFolder;
        }

        var targetPeerFolder = Path.Combine(rootReceivedFolder, cleanPeerName);
        if (!Directory.Exists(targetPeerFolder))
        {
            Directory.CreateDirectory(targetPeerFolder);
        }

        StatusMessage = $"Downloading {item.FileName} to Received\\{cleanPeerName}...";
        var res = await _publicSpaceService.DownloadPublicFileAsync(SelectedPeer, item.Entry, targetPeerFolder);
        StatusMessage = res.IsSuccess
            ? $"✅ Downloaded {item.FileName} to Received\\{cleanPeerName}\\{item.FileName}"
            : $"❌ Download failed: {res.Error}";
    }

    [RelayCommand]
    private void OpenReceivedFolder()
    {
        var rootReceivedFolder = _localProfile.ReceivedFilesRoot;
        if (string.IsNullOrWhiteSpace(rootReceivedFolder))
        {
            rootReceivedFolder = LocalShare.Common.Constants.ReceivedFolder;
        }

        if (SelectedPeer != null)
        {
            string cleanPeerName = string.Concat(SelectedPeer.DisplayName.Split(Path.GetInvalidFileNameChars())).Trim();
            var targetPeerFolder = Path.Combine(rootReceivedFolder, cleanPeerName);
            if (Directory.Exists(targetPeerFolder))
            {
                Process.Start("explorer.exe", targetPeerFolder);
                return;
            }
        }

        if (!Directory.Exists(rootReceivedFolder))
        {
            Directory.CreateDirectory(rootReceivedFolder);
        }
        Process.Start("explorer.exe", rootReceivedFolder);
    }
}
