using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace LocalShare.Common;

public static class NetworkHelpers
{
    public static string GetLocalIpAddress()
    {
        var addresses = GetAllLocalIPv4Addresses();
        return addresses.FirstOrDefault() ?? "127.0.0.1";
    }

    public static List<string> GetAllLocalIPv4Addresses()
    {
        var result = new List<string>();

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                // Skip virtual adapters if possible
                var name = ni.Name.ToLowerInvariant();
                var desc = ni.Description.ToLowerInvariant();
                if (name.Contains("vbox") || name.Contains("virtual") || name.Contains("wsl") || name.Contains("veth") ||
                    desc.Contains("virtual") || desc.Contains("hyper-v") || desc.Contains("vmware"))
                {
                    continue;
                }

                var ipProps = ni.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(addr.Address))
                    {
                        result.Add(addr.Address.ToString());
                    }
                }
            }
        }
        catch { }

        // Fallback: If no physical NICs found, include all active IPv4 NICs
        if (result.Count == 0)
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                        {
                            result.Add(addr.Address.ToString());
                        }
                    }
                }
            }
            catch { }
        }

        if (result.Count == 0)
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint)
                {
                    result.Add(endPoint.Address.ToString());
                }
            }
            catch { }
        }

        return result.Distinct().ToList();
    }

    public static List<IPEndPoint> GetBroadcastEndPoints(int port)
    {
        var endPoints = new List<IPEndPoint>
        {
            // 1. Limited Broadcast
            new IPEndPoint(IPAddress.Broadcast, port),
            // 2. Multicast Group
            new IPEndPoint(IPAddress.Parse(Constants.MulticastGroupAddress), port)
        };

        // 3. Interface-specific Subnet Broadcasts
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var ipProps = ni.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork && addr.IPv4Mask != null)
                    {
                        var ipBytes = addr.Address.GetAddressBytes();
                        var maskBytes = addr.IPv4Mask.GetAddressBytes();

                        if (ipBytes.Length == 4 && maskBytes.Length == 4)
                        {
                            var broadcastBytes = new byte[4];
                            for (int i = 0; i < 4; i++)
                            {
                                broadcastBytes[i] = (byte)(ipBytes[i] | (maskBytes[i] ^ 255));
                            }
                            endPoints.Add(new IPEndPoint(new IPAddress(broadcastBytes), port));
                        }
                    }
                }
            }
        }
        catch { }

        return endPoints.GroupBy(e => e.ToString()).Select(g => g.First()).ToList();
    }
}
