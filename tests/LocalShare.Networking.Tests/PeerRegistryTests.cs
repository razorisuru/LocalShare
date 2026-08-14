using LocalShare.Core.Models;
using LocalShare.Networking.Discovery;
using Xunit;

namespace LocalShare.Networking.Tests;

public class PeerRegistryTests
{
    [Fact]
    public void RegisterOrUpdatePeer_ShouldAddAndTriggerEvents()
    {
        var registry = new PeerRegistry();
        Peer? discoveredPeer = null;

        registry.PeerDiscovered += (s, p) => discoveredPeer = p;

        var peer = new Peer
        {
            DeviceId = "dev-1",
            DisplayName = "Isuru",
            IpAddress = "192.168.1.10"
        };

        registry.RegisterOrUpdatePeer(peer);

        Assert.NotNull(discoveredPeer);
        Assert.Equal("dev-1", discoveredPeer.DeviceId);
        Assert.Single(registry.GetPeers());
    }

    [Fact]
    public void CleanupInactivePeers_ShouldRemoveStalePeers()
    {
        var registry = new PeerRegistry();
        string? lostDeviceId = null;
        registry.PeerLost += (s, id) => lostDeviceId = id;

        var peer = new Peer
        {
            DeviceId = "dev-stale",
            DisplayName = "OldPeer",
            LastSeenAt = DateTime.UtcNow.AddMinutes(-1)
        };

        registry.RegisterOrUpdatePeer(peer);
        registry.CleanupInactivePeers(TimeSpan.FromSeconds(15));

        Assert.Equal("dev-stale", lostDeviceId);
        Assert.Empty(registry.GetPeers());
    }
}
