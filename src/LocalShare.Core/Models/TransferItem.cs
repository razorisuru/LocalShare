using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LocalShare.Core.Models;

public enum TransferDirection
{
    Incoming = 0,
    Outgoing = 1
}

public enum TransferStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Paused = 4,
    Cancelled = 5
}

public class TransferItem : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private TransferDirection _direction;
    private string _peerDeviceId = string.Empty;
    private string _peerDisplayName = string.Empty;
    private string _fileName = string.Empty;
    private long _sizeBytes;
    private long _bytesTransferred;
    private string _sha256 = string.Empty;
    private TransferStatus _status = TransferStatus.Pending;
    private DateTime _startedAt = DateTime.UtcNow;
    private DateTime? _completedAt;
    private string _filePath = string.Empty;
    private string? _chatMessageId;

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

    public TransferDirection Direction
    {
        get => _direction;
        set { if (_direction != value) { _direction = value; OnPropertyChanged(); } }
    }

    public string PeerDeviceId
    {
        get => _peerDeviceId;
        set { if (_peerDeviceId != value) { _peerDeviceId = value; OnPropertyChanged(); } }
    }

    public string PeerDisplayName
    {
        get => _peerDisplayName;
        set { if (_peerDisplayName != value) { _peerDisplayName = value; OnPropertyChanged(); } }
    }

    public string FileName
    {
        get => _fileName;
        set { if (_fileName != value) { _fileName = value; OnPropertyChanged(); } }
    }

    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (_sizeBytes != value)
            {
                _sizeBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressPercentage));
                OnPropertyChanged(nameof(FormattedSize));
                OnPropertyChanged(nameof(FormattedTransferred));
                OnPropertyChanged(nameof(FormattedProgressDetails));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    public long BytesTransferred
    {
        get => _bytesTransferred;
        set
        {
            if (_bytesTransferred != value)
            {
                _bytesTransferred = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressPercentage));
                OnPropertyChanged(nameof(FormattedTransferred));
                OnPropertyChanged(nameof(FormattedProgressDetails));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    public string Sha256
    {
        get => _sha256;
        set { if (_sha256 != value) { _sha256 = value; OnPropertyChanged(); } }
    }

    public TransferStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(CanCancel));
            }
        }
    }

    public DateTime StartedAt
    {
        get => _startedAt;
        set { if (_startedAt != value) { _startedAt = value; OnPropertyChanged(); } }
    }

    public DateTime? CompletedAt
    {
        get => _completedAt;
        set { if (_completedAt != value) { _completedAt = value; OnPropertyChanged(); } }
    }

    public string FilePath
    {
        get => _filePath;
        set { if (_filePath != value) { _filePath = value; OnPropertyChanged(); } }
    }

    public string? ChatMessageId
    {
        get => _chatMessageId;
        set { if (_chatMessageId != value) { _chatMessageId = value; OnPropertyChanged(); } }
    }

    public double ProgressPercentage => SizeBytes > 0 ? Math.Min(100.0, (double)BytesTransferred / SizeBytes * 100.0) : 0;

    public string FormattedSize => FormatBytes(SizeBytes);
    public string FormattedTransferred => FormatBytes(BytesTransferred);

    public string FormattedProgressDetails => $"{FormattedTransferred} / {FormattedSize} ({ProgressPercentage:F0}%)";

    public bool CanCancel => Status == TransferStatus.InProgress || Status == TransferStatus.Pending || Status == TransferStatus.Paused;

    public string StatusText => Status switch
    {
        TransferStatus.Pending => "⏳ Pending",
        TransferStatus.InProgress => $"🚀",
        TransferStatus.Completed => "✅ Completed",
        TransferStatus.Failed => "❌ Failed",
        TransferStatus.Paused => "⏸️ Paused",
        TransferStatus.Cancelled => "⏹️ Cancelled",
        _ => Status.ToString()
    };

    public string StatusColor => Status switch
    {
        TransferStatus.InProgress => "#00A2ED",
        TransferStatus.Completed => "#2ECC71",
        TransferStatus.Failed => "#E74C3C",
        TransferStatus.Paused => "#F39C12",
        TransferStatus.Cancelled => "#E74C3C",
        _ => "#AAAAAA"
    };

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}

public class PublicShareEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public bool IsDirectory { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}
