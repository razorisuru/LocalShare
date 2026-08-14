using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LocalShare.Core.Models;

public class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ConversationId { get; set; } = string.Empty;
    public string SenderDeviceId { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? FileTransferId { get; set; }
    public string? AttachmentFileName { get; set; }
    public long AttachmentSizeBytes { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public bool IsSentByMe { get; set; }
}

public enum ConversationType
{
    Direct = 0,
    Group = 1
}

public class Conversation : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private ConversationType _type = ConversationType.Direct;
    private string _displayName = string.Empty;
    private string? _targetDeviceId;
    private string? _groupId;
    private DateTime _lastMessageAt = DateTime.UtcNow;
    private int _unreadCount;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string Id
    {
        get => _id;
        set { if (_id != value) { _id = value; OnPropertyChanged(); } }
    }

    public ConversationType Type
    {
        get => _type;
        set { if (_type != value) { _type = value; OnPropertyChanged(); } }
    }

    public string DisplayName
    {
        get => _displayName;
        set { if (_displayName != value) { _displayName = value; OnPropertyChanged(); } }
    }

    public string? TargetDeviceId
    {
        get => _targetDeviceId;
        set { if (_targetDeviceId != value) { _targetDeviceId = value; OnPropertyChanged(); } }
    }

    public string? GroupId
    {
        get => _groupId;
        set { if (_groupId != value) { _groupId = value; OnPropertyChanged(); } }
    }

    public DateTime LastMessageAt
    {
        get => _lastMessageAt;
        set { if (_lastMessageAt != value) { _lastMessageAt = value; OnPropertyChanged(); } }
    }

    public int UnreadCount
    {
        get => _unreadCount;
        set { if (_unreadCount != value) { _unreadCount = value; OnPropertyChanged(); } }
    }
}

public class ChatMessagePayload
{
    public string MessageId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string SenderDeviceId { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? FileTransferId { get; set; }
    public string? AttachmentFileName { get; set; }
    public long AttachmentSizeBytes { get; set; }
    public string SentAt { get; set; } = DateTime.UtcNow.ToString("o");
}

public class TypingNotificationRequest
{
    public string SenderDeviceId { get; set; } = string.Empty;
}

