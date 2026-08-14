using LocalShare.Core.Models;
using Xunit;

namespace LocalShare.Core.Tests;

public class ModelTests
{
    [Fact]
    public void Peer_IsOnline_ShouldReturnTrueWhenRecent()
    {
        var peer = new Peer
        {
            LastSeenAt = DateTime.UtcNow
        };

        Assert.True(peer.IsOnline);
    }

    [Fact]
    public void Peer_IsOnline_ShouldReturnFalseWhenStale()
    {
        var peer = new Peer
        {
            LastSeenAt = DateTime.UtcNow.AddMinutes(-1)
        };

        Assert.False(peer.IsOnline);
    }

    [Fact]
    public void TransferItem_ProgressPercentage_ShouldCalculateCorrectly()
    {
        var transfer = new TransferItem
        {
            SizeBytes = 1000,
            BytesTransferred = 500
        };

        Assert.Equal(50.0, transfer.ProgressPercentage);
    }
}
