using Microsoft.Data.Sqlite;
using LocalShare.Core.Models;
using LocalShare.Data;
using LocalShare.Data.Repositories;
using Xunit;

namespace LocalShare.Data.Tests;

public class RepositoryTests
{
    [Fact]
    public async Task DatabaseInitializer_ShouldCreateTablesAndProfile()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_localshare_{Guid.NewGuid():N}.db");
        try
        {
            var dbInit = new DatabaseInitializer(tempDb);
            await dbInit.InitializeAsync();

            var repos = new SqliteRepositories(dbInit);
            var profile = await repos.GetProfileAsync();

            Assert.NotNull(profile);
            Assert.False(string.IsNullOrWhiteSpace(profile.DeviceId));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task PeerRepository_UpsertAndRetrieve_ShouldWork()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_localshare_{Guid.NewGuid():N}.db");
        try
        {
            var dbInit = new DatabaseInitializer(tempDb);
            await dbInit.InitializeAsync();

            var repos = new SqliteRepositories(dbInit);
            var peer = new Peer
            {
                DeviceId = "test-device-123",
                DisplayName = "Kavindu",
                IpAddress = "192.168.1.50",
                HttpPort = 53211,
                HasPublicSpace = true
            };

            await repos.UpsertPeerAsync(peer);
            var peers = await repos.GetAllPeersAsync();

            Assert.Single(peers);
            Assert.Equal("Kavindu", peers[0].DisplayName);
            Assert.True(peers[0].HasPublicSpace);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task MessageRepository_IncomingAndOutgoingMessages_ShouldPreserveConversationNames()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_localshare_{Guid.NewGuid():N}.db");
        try
        {
            var dbInit = new DatabaseInitializer(tempDb);
            await dbInit.InitializeAsync();

            var repos = new SqliteRepositories(dbInit);

            // Peer Bob
            var bobPeer = new Peer
            {
                DeviceId = "bob-device-id",
                DisplayName = "Bob",
                IpAddress = "192.168.1.20",
                HttpPort = 53211
            };
            await repos.UpsertPeerAsync(bobPeer);

            // Alice sends message to Bob
            var outgoingMsg = new Message
            {
                Id = "msg-1",
                ConversationId = "bob-device-id",
                SenderDeviceId = "alice-device-id",
                SenderDisplayName = "Alice",
                Body = "Hello Bob!",
                SentAt = DateTime.UtcNow,
                IsSentByMe = true
            };
            await repos.SaveMessageAsync(outgoingMsg);

            // Check conversation on Alice's side
            var convs = await repos.GetConversationsAsync();
            Assert.Single(convs);
            Assert.Equal("bob-device-id", convs[0].Id);
            Assert.Equal("Bob", convs[0].DisplayName); // DisplayName must be Bob, not overwritten by Alice!
            Assert.Equal("bob-device-id", convs[0].TargetDeviceId);

            // Bob receives message from Alice
            var incomingMsg = new Message
            {
                Id = "msg-2",
                ConversationId = "alice-device-id",
                SenderDeviceId = "alice-device-id",
                SenderDisplayName = "Alice",
                Body = "Hey Alice from Bob",
                SentAt = DateTime.UtcNow.AddSeconds(5),
                IsSentByMe = false
            };
            await repos.SaveMessageAsync(incomingMsg);

            // Verify Alice's messages
            var msgs = await repos.GetMessagesAsync("bob-device-id");
            Assert.Single(msgs);
            Assert.Equal("Hello Bob!", msgs[0].Body);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }
}

