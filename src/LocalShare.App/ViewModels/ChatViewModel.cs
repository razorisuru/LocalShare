using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;

namespace LocalShare.App.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly IChatService _chatService;
    private readonly IDiscoveryService _discoveryService;
    private readonly IPeerRepository _peerRepo;
    private readonly IGroupRepository _groupRepo;
    private readonly ITransferRepository _transferRepo;
    private readonly Profile _localProfile;

    [ObservableProperty]
    private ObservableCollection<Conversation> _conversations = new();

    [ObservableProperty]
    private Conversation? _selectedConversation;

    [ObservableProperty]
    private ObservableCollection<Message> _messages = new();

    [ObservableProperty]
    private string _messageInput = string.Empty;

    [ObservableProperty]
    private string? _selectedAttachmentPath;

    [ObservableProperty]
    private string _typingText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isSending;

    public ChatViewModel(
        IChatService chatService,
        IDiscoveryService discoveryService,
        IPeerRepository peerRepo,
        IGroupRepository groupRepo,
        ITransferRepository transferRepo,
        Profile localProfile)
    {
        _chatService = chatService;
        _discoveryService = discoveryService;
        _peerRepo = peerRepo;
        _groupRepo = groupRepo;
        _transferRepo = transferRepo;
        _localProfile = localProfile;

        _chatService.MessageReceived += OnMessageReceived;
        _chatService.TypingIndicatorReceived += OnTypingReceived;

        _ = LoadConversationsAsync();
    }

    partial void OnStatusMessageChanged(string value)
    {
        HasStatusMessage = !string.IsNullOrWhiteSpace(value);
    }

    partial void OnMessageInputChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && SelectedConversation != null && SelectedConversation.Type == ConversationType.Direct)
        {
            var targetDeviceId = SelectedConversation.TargetDeviceId ?? SelectedConversation.Id;
            var peer = _discoveryService.GetPeerByDeviceId(targetDeviceId);
            if (peer != null)
            {
                _ = _chatService.SendTypingNotificationAsync(peer);
            }
        }
    }

    public async Task LoadConversationsAsync()
    {
        var convs = await _chatService.GetConversationsAsync();
        App.Current?.Dispatcher.Invoke(() =>
        {
            var currentSelectedId = SelectedConversation?.Id ?? SelectedConversation?.TargetDeviceId;

            // Remove conversations no longer present
            for (int i = Conversations.Count - 1; i >= 0; i--)
            {
                if (!convs.Any(c => c.Id == Conversations[i].Id))
                {
                    Conversations.RemoveAt(i);
                }
            }

            // Update existing or add new while preserving references
            for (int i = 0; i < convs.Count; i++)
            {
                var incoming = convs[i];
                var existing = Conversations.FirstOrDefault(c => c.Id == incoming.Id);
                if (existing != null)
                {
                    existing.DisplayName = incoming.DisplayName;
                    existing.LastMessageAt = incoming.LastMessageAt;
                    existing.UnreadCount = incoming.UnreadCount;
                    existing.TargetDeviceId = incoming.TargetDeviceId;
                    existing.GroupId = incoming.GroupId;
                    existing.Type = incoming.Type;

                    int currentIndex = Conversations.IndexOf(existing);
                    if (currentIndex != i && i < Conversations.Count)
                    {
                        Conversations.Move(currentIndex, i);
                    }
                }
                else
                {
                    if (i <= Conversations.Count)
                    {
                        Conversations.Insert(i, incoming);
                    }
                    else
                    {
                        Conversations.Add(incoming);
                    }
                }
            }

            // Restore / maintain SelectedConversation if it was lost
            if (SelectedConversation == null && currentSelectedId != null)
            {
                SelectedConversation = Conversations.FirstOrDefault(c => c.Id == currentSelectedId || c.TargetDeviceId == currentSelectedId);
            }
        });
    }

    public async Task OpenConversationWithPeerAsync(Peer peer)
    {
        await LoadConversationsAsync();
        var existing = Conversations.FirstOrDefault(c =>
            c.Type == ConversationType.Direct &&
            (c.TargetDeviceId == peer.DeviceId || c.Id == peer.DeviceId));

        if (existing != null)
        {
            SelectedConversation = existing;
        }
        else
        {
            var newConv = new Conversation
            {
                Id = peer.DeviceId,
                Type = ConversationType.Direct,
                DisplayName = peer.DisplayName,
                TargetDeviceId = peer.DeviceId,
                LastMessageAt = DateTime.UtcNow
            };
            Conversations.Insert(0, newConv);
            SelectedConversation = newConv;
        }
    }

    partial void OnSelectedConversationChanged(Conversation? value)
    {
        StatusMessage = string.Empty;
        if (value != null)
        {
            _ = LoadMessagesForConversationAsync(value.Id);
        }
    }

    private async Task LoadMessagesForConversationAsync(string conversationId)
    {
        var targetId = SelectedConversation?.TargetDeviceId ?? conversationId;
        var msgs = await _chatService.GetMessagesAsync(targetId);

        if (msgs.Count == 0 && targetId != conversationId)
        {
            msgs = await _chatService.GetMessagesAsync(conversationId);
        }

        App.Current?.Dispatcher.Invoke(() =>
        {
            Messages.Clear();
            foreach (var m in msgs)
            {
                m.IsSentByMe = (m.SenderDeviceId == _localProfile.DeviceId);
                Messages.Add(m);
            }
        });
    }

    [RelayCommand]
    private void SelectAttachment()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog();
        if (dlg.ShowDialog() == true)
        {
            SelectedAttachmentPath = dlg.FileName;
        }
    }

    [RelayCommand]
    private void ClearAttachment()
    {
        SelectedAttachmentPath = null;
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (IsSending) return;
        if (SelectedConversation == null || (string.IsNullOrWhiteSpace(MessageInput) && string.IsNullOrWhiteSpace(SelectedAttachmentPath)))
            return;

        StatusMessage = string.Empty;
        IsSending = true;

        try
        {
            if (SelectedConversation.Type == ConversationType.Direct)
            {
                var targetDeviceId = SelectedConversation.TargetDeviceId ?? SelectedConversation.Id;
                var peer = _discoveryService.GetPeerByDeviceId(targetDeviceId);

                if (peer == null)
                {
                    peer = _discoveryService.GetDiscoveredPeers().FirstOrDefault(p => p.DeviceId == targetDeviceId);
                }

                if (peer == null)
                {
                    var allPeers = await _peerRepo.GetAllPeersAsync();
                    peer = allPeers.FirstOrDefault(p => p.DeviceId == targetDeviceId);
                }

                if (peer == null || string.IsNullOrWhiteSpace(peer.IpAddress))
                {
                    StatusMessage = "❌ Target peer is offline or not found on network.";
                    return;
                }

                var res = await _chatService.SendDirectMessageAsync(peer, MessageInput, SelectedAttachmentPath);
                if (res.IsSuccess && res.Value != null)
                {
                    res.Value.IsSentByMe = true;
                    Messages.Add(res.Value);
                    MessageInput = string.Empty;
                    SelectedAttachmentPath = null;
                    StatusMessage = string.Empty;
                    await LoadConversationsAsync();
                }
                else
                {
                    StatusMessage = $"❌ Direct message failed: {res.Error}";
                }
            }
            else if (SelectedConversation.Type == ConversationType.Group && !string.IsNullOrWhiteSpace(SelectedConversation.GroupId))
            {
                var groups = await _groupRepo.GetAllGroupsAsync();
                var group = groups.FirstOrDefault(g => g.Id == SelectedConversation.GroupId);
                if (group == null)
                {
                    StatusMessage = "❌ Group conversation not found.";
                    return;
                }

                var onlinePeers = _discoveryService.GetDiscoveredPeers();
                var res = await _chatService.SendGroupMessageAsync(group, onlinePeers, MessageInput, SelectedAttachmentPath);
                if (res.IsSuccess && res.Value != null)
                {
                    res.Value.IsSentByMe = true;
                    Messages.Add(res.Value);
                    MessageInput = string.Empty;
                    SelectedAttachmentPath = null;
                    StatusMessage = string.Empty;
                    await LoadConversationsAsync();
                }
                else
                {
                    StatusMessage = $"❌ Group message failed: {res.Error}";
                }
            }
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private async Task OpenFileAttachmentAsync(Message? message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.AttachmentFileName))
            return;

        try
        {
            string? localFilePath = null;

            // 1. Check transfer repository for recorded file path
            if (!string.IsNullOrWhiteSpace(message.FileTransferId))
            {
                var transfer = await _transferRepo.GetTransferByIdAsync(message.FileTransferId);
                if (transfer != null && !string.IsNullOrWhiteSpace(transfer.FilePath) && File.Exists(transfer.FilePath))
                {
                    localFilePath = transfer.FilePath;
                }
            }

            // 2. Check by ChatMessageId
            if (localFilePath == null)
            {
                var allTransfers = await _transferRepo.GetAllTransfersAsync();
                var match = allTransfers.FirstOrDefault(t => t.ChatMessageId == message.Id || t.Id == message.FileTransferId);
                if (match != null && !string.IsNullOrWhiteSpace(match.FilePath) && File.Exists(match.FilePath))
                {
                    localFilePath = match.FilePath;
                }
            }

            // 3. Check in default received folders
            if (localFilePath == null)
            {
                var sanitizedSender = string.Join("_", message.SenderDisplayName.Split(Path.GetInvalidFileNameChars()));
                var candidate = Path.Combine(_localProfile.ReceivedFilesRoot, sanitizedSender, message.AttachmentFileName);
                if (File.Exists(candidate))
                {
                    localFilePath = candidate;
                }
            }

            // 4. If file exists, launch it with default OS handler
            if (localFilePath != null && File.Exists(localFilePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = localFilePath,
                    UseShellExecute = true
                });
            }
            else
            {
                // Fallback: Open Received files folder
                if (Directory.Exists(_localProfile.ReceivedFilesRoot))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _localProfile.ReceivedFilesRoot,
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"⚠️ Could not open file: {ex.Message}";
        }
    }

    private void OnMessageReceived(object? sender, Message msg)
    {
        App.Current?.Dispatcher.Invoke(() =>
        {
            var activeConvId = SelectedConversation?.Id;
            var activeTargetId = SelectedConversation?.TargetDeviceId;

            bool isForCurrentConversation = SelectedConversation != null &&
                (msg.ConversationId == activeConvId ||
                 msg.ConversationId == activeTargetId ||
                 msg.SenderDeviceId == activeTargetId ||
                 msg.SenderDeviceId == activeConvId);

            if (isForCurrentConversation)
            {
                if (!Messages.Any(m => m.Id == msg.Id))
                {
                    msg.IsSentByMe = (msg.SenderDeviceId == _localProfile.DeviceId);
                    Messages.Add(msg);
                }
            }
            _ = LoadConversationsAsync();
        });
    }

    private void OnTypingReceived(object? sender, string senderDeviceId)
    {
        App.Current?.Dispatcher.Invoke(() =>
        {
            if (SelectedConversation != null && (SelectedConversation.TargetDeviceId == senderDeviceId || SelectedConversation.Id == senderDeviceId))
            {
                TypingText = "Peer is typing...";
                Task.Delay(3000).ContinueWith(_ => App.Current?.Dispatcher.Invoke(() => TypingText = string.Empty));
            }
        });
    }
}
