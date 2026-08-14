using System.Collections.Concurrent;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;

namespace LocalShare.Networking.Discovery;

public class PeerRegistry
{
    private readonly ConcurrentDictionary<string, Peer> _peers = new();
    private readonly IPeerRepository? _peerRepository;

    public event EventHandler<Peer>? PeerDiscovered;
    public event EventHandler<Peer>? PeerUpdated;
    public event EventHandler<string>? PeerLost;

    public PeerRegistry(IPeerRepository? peerRepository = null)
    {
        _peerRepository = peerRepository;
    }

    public void RegisterOrUpdatePeer(Peer peer)
    {
        bool isNew = !_peers.ContainsKey(peer.DeviceId);
        _peers[peer.DeviceId] = peer;

        if (isNew)
        {
            PeerDiscovered?.Invoke(this, peer);
        }
        else
        {
            PeerUpdated?.Invoke(this, peer);
        }

        _ = _peerRepository?.UpsertPeerAsync(peer);
    }

    public IReadOnlyList<Peer> GetPeers() => _peers.Values.ToList();

    public Peer? GetPeer(string deviceId)
    {
        _peers.TryGetValue(deviceId, out var peer);
        return peer;
    }

    public void CleanupInactivePeers(TimeSpan timeout)
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _peers)
        {
            if (now - kvp.Value.LastSeenAt > timeout)
            {
                if (_peers.TryRemove(kvp.Key, out _))
                {
                    PeerLost?.Invoke(this, kvp.Key);
                }
            }
        }
    }
}
