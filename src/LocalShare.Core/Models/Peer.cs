using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LocalShare.Core.Models;

public class Peer : INotifyPropertyChanged
{
    private string _deviceId = string.Empty;
    private string _displayName = string.Empty;
    private string _avatarHash = string.Empty;
    private string _accentColor = "#0078D4";
    private string _ipAddress = string.Empty;
    private int _httpPort = 53211;
    private bool _hasPublicSpace;
    private DateTime _lastSeenAt = DateTime.UtcNow;
    private string _protocolVersion = "1.0.0";
    private string _appVersion = "1.0.0";
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string DeviceId
    {
        get => _deviceId;
        set { if (_deviceId != value) { _deviceId = value; OnPropertyChanged(); } }
    }

    public string DisplayName
    {
        get => _displayName;
        set { if (_displayName != value) { _displayName = value; OnPropertyChanged(); } }
    }

    public string AvatarHash
    {
        get => _avatarHash;
        set { if (_avatarHash != value) { _avatarHash = value; OnPropertyChanged(); } }
    }

    public string AccentColor
    {
        get => _accentColor;
        set { if (_accentColor != value) { _accentColor = value; OnPropertyChanged(); } }
    }

    public string IpAddress
    {
        get => _ipAddress;
        set { if (_ipAddress != value) { _ipAddress = value; OnPropertyChanged(); } }
    }

    public int HttpPort
    {
        get => _httpPort;
        set { if (_httpPort != value) { _httpPort = value; OnPropertyChanged(); } }
    }

    public bool HasPublicSpace
    {
        get => _hasPublicSpace;
        set { if (_hasPublicSpace != value) { _hasPublicSpace = value; OnPropertyChanged(); } }
    }

    public DateTime LastSeenAt
    {
        get => _lastSeenAt;
        set
        {
            if (_lastSeenAt != value)
            {
                _lastSeenAt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOnline));
            }
        }
    }

    public string ProtocolVersion
    {
        get => _protocolVersion;
        set { if (_protocolVersion != value) { _protocolVersion = value; OnPropertyChanged(); } }
    }

    public string AppVersion
    {
        get => _appVersion;
        set { if (_appVersion != value) { _appVersion = value; OnPropertyChanged(); } }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public bool IsOnline => (DateTime.UtcNow - LastSeenAt).TotalSeconds <= 15;
}
