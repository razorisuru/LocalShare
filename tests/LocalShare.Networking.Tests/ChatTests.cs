using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;
using LocalShare.Networking.Chat;
using Xunit;

namespace LocalShare.Networking.Tests;

public class MockMessageRepository : IMessageRepository
{
    public List<Message> SavedMessages { get; } = new();
    public List<Conversation> Conversations { get; } = new();

    public Task SaveMessageAsync(Message message)
    {
        SavedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Message>> GetMessagesAsync(string conversationId, int limit = 50)
    {
        return Task.FromResult<IReadOnlyList<Message>>(SavedMessages.Where(m => m.ConversationId == conversationId).ToList());
    }

    public Task<IReadOnlyList<Conversation>> GetConversationsAsync()
    {
        return Task.FromResult<IReadOnlyList<Conversation>>(Conversations);
    }

    public Task MarkAsReadAsync(string conversationId)
    {
        return Task.CompletedTask;
    }
}

public class MockTransferService : ITransferService
{
    public event EventHandler<TransferItem>? TransferProgressChanged;
    public event EventHandler<TransferItem>? FileReceived;

    public Task<LocalShare.Common.Result<TransferItem>> SendFileAsync(Peer targetPeer, string filePath, string? chatMessageId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LocalShare.Common.Result<TransferItem>.Failure("Not implemented"));
    }

    public Task<LocalShare.Common.Result<TransferItem>> InitiateIncomingTransferAsync(string transferId, string senderDeviceId, string senderDisplayName, string fileName, long sizeBytes, string sha256, string? chatMessageId)
    {
        return Task.FromResult(LocalShare.Common.Result<TransferItem>.Failure("Not implemented"));
    }

    public Task<LocalShare.Common.Result> ReceiveChunkAsync(string transferId, long offset, byte[] chunkData, int count)
    {
        return Task.FromResult(LocalShare.Common.Result.Success());
    }

    public Task<LocalShare.Common.Result> PauseTransferAsync(string transferId) => Task.FromResult(LocalShare.Common.Result.Success());
    public Task<LocalShare.Common.Result> ResumeTransferAsync(string transferId) => Task.FromResult(LocalShare.Common.Result.Success());
    public Task<LocalShare.Common.Result> CancelTransferAsync(string transferId) => Task.FromResult(LocalShare.Common.Result.Success());
    public Task<IReadOnlyList<TransferItem>> GetTransferLogsAsync() => Task.FromResult<IReadOnlyList<TransferItem>>(new List<TransferItem>());
    public Task ClearAllTransferLogsAsync() => Task.CompletedTask;
    public TransferItem? GetTransfer(string transferId) => null;
}

public class ChatTests
{
    [Fact]
    public async Task ChatService_ReceiveDirectMessageAsync_ShouldSaveMessageAndRaiseEvent()
    {
        var localProfile = new Profile
        {
            DeviceId = "bob-id",
            DisplayName = "Bob"
        };
        var mockRepo = new MockMessageRepository();
        var mockTransfer = new MockTransferService();
        var chatService = new ChatService(localProfile, mockRepo, mockTransfer);

        Message? receivedEventMsg = null;
        chatService.MessageReceived += (s, msg) => receivedEventMsg = msg;

        var payload = new ChatMessagePayload
        {
            MessageId = "msg-101",
            ConversationId = "bob-id",
            SenderDeviceId = "alice-id",
            SenderDisplayName = "Alice",
            Body = "Hey Bob!",
            SentAt = DateTime.UtcNow.ToString("o")
        };

        var result = await chatService.ReceiveDirectMessageAsync(payload);

        Assert.True(result.IsSuccess);
        Assert.Single(mockRepo.SavedMessages);
        Assert.Equal("alice-id", mockRepo.SavedMessages[0].ConversationId);
        Assert.Equal("Hey Bob!", mockRepo.SavedMessages[0].Body);
        Assert.False(mockRepo.SavedMessages[0].IsSentByMe);

        Assert.NotNull(receivedEventMsg);
        Assert.Equal("msg-101", receivedEventMsg.Id);
        Assert.Equal("alice-id", receivedEventMsg.ConversationId);
    }

    [Fact]
    public async Task ChatService_ReceiveTypingAsync_ShouldRaiseTypingEvent()
    {
        var localProfile = new Profile { DeviceId = "bob-id", DisplayName = "Bob" };
        var mockRepo = new MockMessageRepository();
        var mockTransfer = new MockTransferService();
        var chatService = new ChatService(localProfile, mockRepo, mockTransfer);

        string? typingSender = null;
        chatService.TypingIndicatorReceived += (s, sender) => typingSender = sender;

        await chatService.ReceiveTypingAsync("alice-id");

        Assert.Equal("alice-id", typingSender);
    }

    [Fact]
    public async Task ChatService_ReceiveDirectMessageWithAttachment_ShouldSaveAttachmentMetadata()
    {
        var localProfile = new Profile { DeviceId = "bob-id", DisplayName = "Bob" };
        var mockRepo = new MockMessageRepository();
        var mockTransfer = new MockTransferService();
        var chatService = new ChatService(localProfile, mockRepo, mockTransfer);

        var payload = new ChatMessagePayload
        {
            MessageId = "msg-attachment-1",
            ConversationId = "bob-id",
            SenderDeviceId = "alice-id",
            SenderDisplayName = "Alice",
            Body = "Check this document",
            FileTransferId = "transfer-999",
            AttachmentFileName = "document.pdf",
            AttachmentSizeBytes = 1048576,
            SentAt = DateTime.UtcNow.ToString("o")
        };

        var result = await chatService.ReceiveDirectMessageAsync(payload);

        Assert.True(result.IsSuccess);
        Assert.Single(mockRepo.SavedMessages);
        var saved = mockRepo.SavedMessages[0];
        Assert.Equal("document.pdf", saved.AttachmentFileName);
        Assert.Equal("transfer-999", saved.FileTransferId);
        Assert.Equal(1048576, saved.AttachmentSizeBytes);
    }
}

