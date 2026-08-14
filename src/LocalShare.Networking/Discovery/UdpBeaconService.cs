using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LocalShare.Common;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;

namespace LocalShare.Networking.Discovery;

public class UdpBeaconPayload
{
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarHash { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#0078D4";
    public string Ip { get; set; } = string.Empty;
    public int HttpPort { get; set; } = 53211;
    public bool HasPublicSpace { get; set; }
    public string ProtocolVersion { get; set; } = Constants.ProtocolVersion;
    public string AppVersion { get; set; } = Constants.AppVersion;
}

public class UdpBeaconService : IDiscoveryService
{
    private readonly Profile _localProfile;
    private readonly PeerRegistry _peerRegistry;
    private readonly SubnetScannerService _subnetScanner;
    private UdpClient? _udpListener;
    private CancellationTokenSource? _cts;
    private Task? _broadcastTask;
    private Task? _listenTask;
    private Task? _scanTask;
    private Task? _cleanupTask;

    public event EventHandler<Peer>? PeerDiscovered;
    public event EventHandler<Peer>? PeerUpdated;
    public event EventHandler<string>? PeerLost;

    public UdpBeaconService(Profile localProfile, PeerRegistry peerRegistry)
    {
        _localProfile = localProfile;
        _peerRegistry = peerRegistry;
        _subnetScanner = new SubnetScannerService(localProfile, peerRegistry);

        _peerRegistry.PeerDiscovered += (s, p) => PeerDiscovered?.Invoke(this, p);
        _peerRegistry.PeerUpdated += (s, p) => PeerUpdated?.Invoke(this, p);
        _peerRegistry.PeerLost += (s, id) => PeerLost?.Invoke(this, id);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = new CancellationTokenSource();

        // 1. Try binding UDP Listener safely with port fallback
        int[] portsToTry = new[] { Constants.DefaultUdpPort, 53212, 53214, 0 };
        foreach (var port in portsToTry)
        {
            try
            {
                var udp = new UdpClient();
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));

                var multicastAddr = IPAddress.Parse(Constants.MulticastGroupAddress);
                foreach (var localIp in NetworkHelpers.GetAllLocalIPv4Addresses())
                {
                    try
                    {
                        if (IPAddress.TryParse(localIp, out var ip))
                        {
                            udp.JoinMulticastGroup(multicastAddr, ip);
                        }
                    }
                    catch { }
                }

                _udpListener = udp;
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UDP Port {port} bind failed: {ex.Message}");
            }
        }

        // 2. Launch background discovery tasks safely
        _broadcastTask = Task.Run(() => BroadcastLoopAsync(_cts.Token), _cts.Token);
        if (_udpListener != null)
        {
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
        }
        _scanTask = Task.Run(() => PeriodicSubnetScanLoopAsync(_cts.Token), _cts.Token);
        _cleanupTask = Task.Run(() => CleanupLoopAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _udpListener?.Close();
            _udpListener?.Dispose();
        }
        await Task.CompletedTask;
    }

    public IReadOnlyList<Peer> GetDiscoveredPeers() => _peerRegistry.GetPeers();

    public Peer? GetPeerByDeviceId(string deviceId) => _peerRegistry.GetPeer(deviceId);

    public async Task ScanSubnetNowAsync(CancellationToken cancellationToken = default)
    {
        await _subnetScanner.ScanSubnetAsync(cancellationToken);
    }

    public async Task<Result<Peer>> ConnectDirectIpAsync(string ipAddress, int port = 53211, CancellationToken cancellationToken = default)
    {
        return await _subnetScanner.ConnectDirectIpAsync(ipAddress, port, cancellationToken);
    }

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        var targetEndPoints = NetworkHelpers.GetBroadcastEndPoints(Constants.DefaultUdpPort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var localIps = NetworkHelpers.GetAllLocalIPv4Addresses();
                var primaryIp = localIps.FirstOrDefault() ?? NetworkHelpers.GetLocalIpAddress();

                var payload = new UdpBeaconPayload
                {
                    DeviceId = _localProfile.DeviceId,
                    DisplayName = _localProfile.DisplayName,
                    AvatarHash = string.Empty,
                    AccentColor = _localProfile.AccentColor,
                    Ip = primaryIp,
                    HttpPort = _localProfile.HttpPort,
                    HasPublicSpace = !string.IsNullOrWhiteSpace(_localProfile.PublicSpacePath) && Directory.Exists(_localProfile.PublicSpacePath),
                    ProtocolVersion = _localProfile.ProtocolVersion,
                    AppVersion = _localProfile.AppVersion
                };

                var json = JsonSerializer.Serialize(payload);
                var bytes = Encoding.UTF8.GetBytes(json);

                foreach (var localIpStr in localIps)
                {
                    if (!IPAddress.TryParse(localIpStr, out var localIp)) continue;

                    try
                    {
                        using var sender = new UdpClient(new IPEndPoint(localIp, 0));
                        sender.EnableBroadcast = true;

                        foreach (var targetEp in targetEndPoints)
                        {
                            try
                            {
                                await sender.SendAsync(bytes, bytes.Length, targetEp);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                try
                {
                    using var globalSender = new UdpClient();
                    globalSender.EnableBroadcast = true;
                    foreach (var targetEp in targetEndPoints)
                    {
                        try
                        {
                            await globalSender.SendAsync(bytes, bytes.Length, targetEp);
                        }
                        catch { }
                    }
                }
                catch { }
            }
            catch { }

            await Task.Delay(2000, ct).ConfigureAwait(false);
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udpListener != null)
        {
            try
            {
                var result = await _udpListener.ReceiveAsync(ct);
                var json = Encoding.UTF8.GetString(result.Buffer);
                var payload = JsonSerializer.Deserialize<UdpBeaconPayload>(json);

                if (payload != null && !string.IsNullOrWhiteSpace(payload.DeviceId) && payload.DeviceId != _localProfile.DeviceId)
                {
                    var peerIp = string.IsNullOrWhiteSpace(payload.Ip) ? result.RemoteEndPoint.Address.ToString() : payload.Ip;

                    var peer = new Peer
                    {
                        DeviceId = payload.DeviceId,
                        DisplayName = payload.DisplayName,
                        AvatarHash = payload.AvatarHash,
                        AccentColor = payload.AccentColor,
                        IpAddress = peerIp,
                        HttpPort = payload.HttpPort,
                        HasPublicSpace = payload.HasPublicSpace,
                        LastSeenAt = DateTime.UtcNow,
                        ProtocolVersion = payload.ProtocolVersion,
                        AppVersion = payload.AppVersion
                    };

                    _peerRegistry.RegisterOrUpdatePeer(peer);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore corrupt packet
            }
        }
    }

    private async Task PeriodicSubnetScanLoopAsync(CancellationToken ct)
    {
        await Task.Delay(1000, ct).ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _subnetScanner.ScanSubnetAsync(ct);
            }
            catch { }

            await Task.Delay(10000, ct).ConfigureAwait(false);
        }
    }

    private async Task CleanupLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _peerRegistry.CleanupInactivePeers(TimeSpan.FromSeconds(30));
            }
            catch { }

            await Task.Delay(5000, ct).ConfigureAwait(false);
        }
    }
}
