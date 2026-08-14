using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace LocalShare.App.Helpers;

public static class FirewallHelper
{
    /// <summary>
    /// Checks if current process is running with Administrator privileges.
    /// Only attempts netsh firewall registration if elevated, avoiding AV/Defender behavioral warnings on standard user runs.
    /// </summary>
    public static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static void RegisterFirewallRulesIfElevated()
    {
        if (!IsAdministrator())
        {
            // Standard non-elevated user mode. Windows automatically prompts standard firewall dialog on socket bind.
            return;
        }

        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return;

            var ruleName = "LocalShare P2P LAN Network";

            var tcpArgs = $"advfirewall firewall add rule name=\"{ruleName} (TCP)\" dir=in action=allow program=\"{exePath}\" enable=yes profile=any protocol=TCP";
            RunNetshCommand(tcpArgs);

            var udpArgs = $"advfirewall firewall add rule name=\"{ruleName} (UDP)\" dir=in action=allow program=\"{exePath}\" enable=yes profile=any protocol=UDP";
            RunNetshCommand(udpArgs);
        }
        catch
        {
            // Ignore firewall registration errors
        }
    }

    private static void RunNetshCommand(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var proc = Process.Start(startInfo);
            proc?.WaitForExit(2000);
        }
        catch { }
    }
}
