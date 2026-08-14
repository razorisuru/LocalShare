using LocalShare.Common;
using LocalShare.Core.Models;

namespace LocalShare.Core.Interfaces;

public interface IDiscoveryService
{
    event EventHandler<Peer>? PeerDiscovered;
    event EventHandler<Peer>? PeerUpdated;
    event EventHandler<string>? PeerLost;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<Peer> GetDiscoveredPeers();
    Peer? GetPeerByDeviceId(string deviceId);
    Task ScanSubnetNowAsync(CancellationToken cancellationToken = default);
    Task<Result<Peer>> ConnectDirectIpAsync(string ipAddress, int port = 53211, CancellationToken cancellationToken = default);
}
