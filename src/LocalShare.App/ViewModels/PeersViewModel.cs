using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;

namespace LocalShare.App.ViewModels;

public partial class PeersViewModel : ObservableObject
{
    private readonly IDiscoveryService _discoveryService;
    private readonly ITransferService _transferService;

    public Func<Peer, Task>? RequestStartChat;
    public Action<Peer>? RequestOpenPublicSpace;

    [ObservableProperty]
    private ObservableCollection<Peer> _peers = new();

    [ObservableProperty]
    private Peer? _selectedPeer;

    [ObservableProperty]
    private ObservableCollection<StagedFile> _stagedFiles = new();

    [ObservableProperty]
    private ObservableCollection<TransferItem> _activeTransfers = new();

    [ObservableProperty]
    private string _statusMessage = "Listening & scanning LAN network...";

    [ObservableProperty]
    private string _manualIpInput = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private bool _showLiveTransfers = true;

    public PeersViewModel(IDiscoveryService discoveryService, ITransferService transferService)
    {
        _discoveryService = discoveryService;
        _transferService = transferService;

        _discoveryService.PeerDiscovered += OnPeerDiscovered;
        _discoveryService.PeerUpdated += OnPeerUpdated;
        _discoveryService.PeerLost += OnPeerLost;

        _transferService.TransferProgressChanged += OnTransferProgressChanged;

        LoadInitialPeers();
        _ = LoadInitialTransfersAsync();
    }

    private void LoadInitialPeers()
    {
        Peers.Clear();
        foreach (var p in _discoveryService.GetDiscoveredPeers())
        {
            Peers.Add(p);
        }
        if (Peers.Count > 0 && SelectedPeer == null)
        {
            SelectedPeer = Peers.First();
        }
        UpdateStatus();
    }

    private void OnPeerDiscovered(object? sender, Peer peer)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            if (!Peers.Any(p => p.DeviceId == peer.DeviceId))
            {
                Peers.Add(peer);
            }
            if (SelectedPeer == null)
            {
                SelectedPeer = peer;
            }
            UpdateStatus();
        });
    }

    private void OnPeerUpdated(object? sender, Peer peer)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            var existing = Peers.FirstOrDefault(p => p.DeviceId == peer.DeviceId);
            if (existing != null)
            {
                peer.IsSelected = existing.IsSelected;
                int index = Peers.IndexOf(existing);
                Peers[index] = peer;
            }
            UpdateStatus();
        });
    }

    private void OnPeerLost(object? sender, string deviceId)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            var existing = Peers.FirstOrDefault(p => p.DeviceId == deviceId);
            if (existing != null)
            {
                Peers.Remove(existing);
            }
            if (SelectedPeer?.DeviceId == deviceId)
            {
                SelectedPeer = Peers.FirstOrDefault();
            }
            UpdateStatus();
        });
    }

    private async Task LoadInitialTransfersAsync()
    {
        var logs = await _transferService.GetTransferLogsAsync();
        App.Current.Dispatcher.Invoke(() =>
        {
            ActiveTransfers.Clear();
            foreach (var t in logs.Take(15)) ActiveTransfers.Add(t);
        });
    }

    private void OnTransferProgressChanged(object? sender, TransferItem item)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            var existing = ActiveTransfers.FirstOrDefault(t => t.Id == item.Id);
            if (existing != null)
            {
                existing.BytesTransferred = item.BytesTransferred;
                existing.Status = item.Status;
                existing.CompletedAt = item.CompletedAt;
            }
            else
            {
                ActiveTransfers.Insert(0, item);
            }
        });
    }

    private void UpdateStatus()
    {
        if (IsScanning || IsSending) return;

        int selectedCount = Peers.Count(p => p.IsSelected);
        StatusMessage = Peers.Count > 0
            ? $"{Peers.Count} LAN peer(s) online ({selectedCount} selected for sending)"
            : "Listening & scanning for LAN peers...";
    }

    public void HandleDroppedFiles(string[] filePaths, Peer? targetPeer = null)
    {
        if (targetPeer != null)
        {
            SelectedPeer = targetPeer;
            targetPeer.IsSelected = true;
        }

        foreach (var path in filePaths)
        {
            if (File.Exists(path) && !StagedFiles.Any(f => f.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                var info = new FileInfo(path);
                StagedFiles.Add(new StagedFile
                {
                    FilePath = path,
                    FileName = info.Name,
                    SizeBytes = info.Length
                });
            }
        }

        var selectedPeers = Peers.Where(p => p.IsSelected).ToList();
        var targetText = selectedPeers.Count > 0 ? $"{selectedPeers.Count} peer(s)" : "target peer";
        StatusMessage = $"Added {StagedFiles.Count} file(s) to queue for {targetText}.";
    }

    [RelayCommand]
    private void ToggleLiveTransfers()
    {
        ShowLiveTransfers = !ShowLiveTransfers;
    }

    [RelayCommand]
    private async Task ClearActiveTransfersHistoryAsync()
    {
        await _transferService.ClearAllTransferLogsAsync();
        ActiveTransfers.Clear();
        StatusMessage = "Cleared all transfer records!";
    }

    [RelayCommand]
    private async Task CancelTransferAsync(TransferItem? transfer)
    {
        if (transfer != null)
        {
            var res = await _transferService.CancelTransferAsync(transfer.Id);
            if (res.IsSuccess)
            {
                StatusMessage = $"Cancelled transfer: {transfer.FileName}";
            }
        }
    }

    [RelayCommand]
    private void SelectAllPeers()
    {
        foreach (var p in Peers) p.IsSelected = true;
        UpdateStatus();
    }

    [RelayCommand]
    private void DeselectAllPeers()
    {
        foreach (var p in Peers) p.IsSelected = false;
        UpdateStatus();
    }

    [RelayCommand]
    private void TogglePeerSelection(Peer? peer)
    {
        if (peer != null)
        {
            peer.IsSelected = !peer.IsSelected;
            SelectedPeer = peer;
            UpdateStatus();
        }
    }

    [RelayCommand]
    private async Task StartChatWithPeerAsync(Peer? peer)
    {
        if (peer == null) return;
        SelectedPeer = peer;

        if (RequestStartChat != null)
        {
            await RequestStartChat.Invoke(peer);
        }
    }

    [RelayCommand]
    private void OpenPublicSpaceForPeer(Peer? peer)
    {
        if (peer == null) return;
        SelectedPeer = peer;

        RequestOpenPublicSpace?.Invoke(peer);
    }

    [RelayCommand]
    private void AddStagedFiles()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Files to Send",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            HandleDroppedFiles(openFileDialog.FileNames);
        }
    }

    [RelayCommand]
    private void PasteFromClipboard()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsImage())
            {
                var bitmap = System.Windows.Clipboard.GetImage();
                if (bitmap != null)
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "LocalShare_Screenshots");
                    if (!Directory.Exists(tempDir))
                    {
                        Directory.CreateDirectory(tempDir);
                    }

                    string fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    string filePath = Path.Combine(tempDir, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                        encoder.Save(stream);
                    }

                    HandleDroppedFiles(new[] { filePath });
                    StatusMessage = $"📋 Pasted screenshot ({fileName}) into send queue!";
                }
            }
            else if (System.Windows.Clipboard.ContainsFileDropList())
            {
                var fileList = System.Windows.Clipboard.GetFileDropList();
                if (fileList != null && fileList.Count > 0)
                {
                    var files = new string[fileList.Count];
                    fileList.CopyTo(files, 0);
                    HandleDroppedFiles(files);
                    StatusMessage = $"📋 Pasted {files.Length} file(s) from clipboard.";
                }
            }
            else
            {
                StatusMessage = "No image or file found in Windows clipboard to paste.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error pasting from clipboard: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveStagedFile(StagedFile? file)
    {
        if (file != null)
        {
            StagedFiles.Remove(file);
        }
    }

    [RelayCommand]
    private void ClearStagedFiles()
    {
        StagedFiles.Clear();
    }

    [RelayCommand]
    private async Task SendStagedFilesNowAsync()
    {
        var targetPeers = Peers.Where(p => p.IsSelected).ToList();

        if (targetPeers.Count == 0)
        {
            StatusMessage = "Please select at least one peer to send files to!";
            return;
        }

        if (StagedFiles.Count == 0)
        {
            StatusMessage = "No files added! Click 'Add Files' or Drag & Drop files first.";
            return;
        }

        IsSending = true;
        var filesToSend = StagedFiles.ToList();
        StagedFiles.Clear();

        int totalTransfers = filesToSend.Count * targetPeers.Count;
        int successCount = 0;

        foreach (var targetPeer in targetPeers)
        {
            foreach (var file in filesToSend)
            {
                StatusMessage = $"Sending {file.FileName} ({file.FormattedSize}) to {targetPeer.DisplayName}...";
                var res = await _transferService.SendFileAsync(targetPeer, file.FilePath);
                if (res.IsSuccess)
                {
                    successCount++;
                }
                else
                {
                    StatusMessage = $"Failed to send {file.FileName} to {targetPeer.DisplayName}: {res.Error}";
                }
            }
        }

        IsSending = false;
        StatusMessage = $"Successfully sent {successCount} of {totalTransfers} file transfer(s) across {targetPeers.Count} peer(s)!";
    }

    [RelayCommand]
    private async Task ScanNetworkAsync()
    {
        IsScanning = true;
        StatusMessage = "Scanning local network range for LocalShare peers...";
        try
        {
            await _discoveryService.ScanSubnetNowAsync();
            UpdateStatus();
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ConnectDirectIpAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualIpInput)) return;

        StatusMessage = $"Connecting directly to {ManualIpInput}...";
        var res = await _discoveryService.ConnectDirectIpAsync(ManualIpInput.Trim());
        if (res.IsSuccess && res.Value != null)
        {
            StatusMessage = $"Successfully connected to {res.Value.DisplayName} ({res.Value.IpAddress})!";
            res.Value.IsSelected = true;
            SelectedPeer = res.Value;
            ManualIpInput = string.Empty;
        }
        else
        {
            StatusMessage = $"Direct connection failed: {res.Error}";
        }
    }
}
