using System.Net.Http.Json;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;
using LocalShare.Common;

namespace LocalShare.Networking.Discovery;

public class SubnetScannerService
{
    private readonly Profile _localProfile;
    private readonly PeerRegistry _peerRegistry;
    private readonly HttpClient _httpClient;

    public SubnetScannerService(Profile localProfile, PeerRegistry peerRegistry)
    {
        _localProfile = localProfile;
        _peerRegistry = peerRegistry;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(800) };
    }

    public async Task ScanSubnetAsync(CancellationToken cancellationToken = default)
    {
        var localIps = NetworkHelpers.GetAllLocalIPv4Addresses();

        foreach (var localIp in localIps)
        {
            var parts = localIp.Split('.');
            if (parts.Length != 4) continue;

            var subnetPrefix = $"{parts[0]}.{parts[1]}.{parts[2]}";
            var tasks = new List<Task>();
            using var semaphore = new SemaphoreSlim(40);

            for (int host = 1; host <= 254; host++)
            {
                var targetIp = $"{subnetPrefix}.{host}";
                if (targetIp == localIp) continue;

                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        await CheckPeerHttpProfileAsync(targetIp, cancellationToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public async Task<Result<Peer>> ConnectDirectIpAsync(string ipAddress, int port = 53211, CancellationToken cancellationToken = default)
    {
        var peer = await CheckPeerHttpProfileAsync(ipAddress, cancellationToken, port);
        if (peer != null)
        {
            return Result<Peer>.Success(peer);
        }
        return Result<Peer>.Failure($"Could not connect to peer at {ipAddress}:{port}");
    }

    private async Task<Peer?> CheckPeerHttpProfileAsync(string ipAddress, CancellationToken ct, int port = 53211)
    {
        try
        {
            var url = $"http://{ipAddress}:{port}/api/profile";
            var profilePayload = await _httpClient.GetFromJsonAsync<Profile>(url, ct);

            if (profilePayload != null && !string.IsNullOrWhiteSpace(profilePayload.DeviceId) && profilePayload.DeviceId != _localProfile.DeviceId)
            {
                var peer = new Peer
                {
                    DeviceId = profilePayload.DeviceId,
                    DisplayName = profilePayload.DisplayName,
                    AvatarHash = string.Empty,
                    AccentColor = profilePayload.AccentColor,
                    IpAddress = ipAddress,
                    HttpPort = profilePayload.HttpPort > 0 ? profilePayload.HttpPort : port,
                    HasPublicSpace = !string.IsNullOrWhiteSpace(profilePayload.PublicSpacePath),
                    LastSeenAt = DateTime.UtcNow,
                    ProtocolVersion = profilePayload.ProtocolVersion,
                    AppVersion = profilePayload.AppVersion
                };

                _peerRegistry.RegisterOrUpdatePeer(peer);
                return peer;
            }
        }
        catch
        {
            // Host offline or not running LocalShare
        }

        return null;
    }
}
