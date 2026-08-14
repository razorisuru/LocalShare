namespace LocalShare.Common;

public static class Constants
{
    public const string AppName = "LocalShare";
    public const string ProtocolVersion = "1.0.0";
    public const string AppVersion = "1.0.0";

    public const int DefaultUdpPort = 53210;
    public const string MulticastGroupAddress = "239.255.10.10";
    public const int DefaultHttpPort = 53211;

    public static string AppDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalShare");

    public static string ProfileFolder => Path.Combine(AppDataRoot, "Profile");
    public static string ReceivedFolder => Path.Combine(AppDataRoot, "Received");
    public static string ChatAttachmentsFolder => Path.Combine(AppDataRoot, "ChatAttachments");
    public static string DatabasePath => Path.Combine(AppDataRoot, "localshare.db");
}
