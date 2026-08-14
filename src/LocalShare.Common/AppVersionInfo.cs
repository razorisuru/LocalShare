using System.Reflection;

namespace LocalShare.Common;

public static class AppVersionInfo
{
    private static string? _version;

    public static string Version
    {
        get
        {
            if (_version == null)
            {
                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(infoVersion))
                {
                    _version = infoVersion.Split('+')[0];
                }
                else
                {
                    var ver = assembly.GetName().Version;
                    _version = ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
                }
            }
            return _version;
        }
    }

    public static string DisplayVersion => $"v{Version}";
}
