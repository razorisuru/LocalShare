namespace LocalShare.Core.Models;

public class Profile
{
    public string DeviceId { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = Environment.MachineName;
    public string AvatarPath { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#0078D4";
    public string? PublicSpacePath { get; set; }
    public string ReceivedFilesRoot { get; set; } = string.Empty;
    public int HttpPort { get; set; } = 53211;
    public bool EnableNotifications { get; set; } = true;
    public string ProtocolVersion { get; set; } = Common.Constants.ProtocolVersion;
    public string AppVersion { get; set; } = Common.Constants.AppVersion;
}
