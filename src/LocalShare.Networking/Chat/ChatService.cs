using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using LocalShare.Common;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;

namespace LocalShare.Networking.Chat;

public class ChatService : IChatService
{
    private readonly Profile _localProfile;
    private readonly IMessageRepository _messageRepo;
    private readonly ITransferService _transferService;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, HubConnection> _hubConnections = new();

    public event EventHandler<Message>? MessageReceived;
    public event EventHandler<string>? TypingIndicatorReceived;

    public ChatService(Profile localProfile, IMessageRepository messageRepo, ITransferService transferService, HttpClient? httpClient = null)
    {
        _localProfile = localProfile;
        _messageRepo = messageRepo;
        _transferService = transferService;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<Result<Message>> SendDirectMessageAsync(Peer targetPeer, string body, string? attachmentPath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var msgId = Guid.NewGuid().ToString("N");
            var conversationId = targetPeer.DeviceId;
            var sentAt = DateTime.UtcNow;

            string? transferId = null;
            string? fileName = null;
            long sizeBytes = 0;

            if (!string.IsNullOrWhiteSpace(attachmentPath) && File.Exists(attachmentPath))
            {
                var sendRes = await _transferService.SendFileAsync(targetPeer, attachmentPath, msgId, cancellationToken);
                if (!sendRes.IsSuccess || sendRes.Value == null)
                {
                    return Result<Message>.Failure($"Failed to transfer attachment: {sendRes.Error}");
                }
                transferId = sendRes.Value.Id;
                fileName = sendRes.Value.FileName;
                sizeBytes = sendRes.Value.SizeBytes;
            }

            var payload = new ChatMessagePayload
            {
                MessageId = msgId,
                ConversationId = conversationId,
                SenderDeviceId = _localProfile.DeviceId,
                SenderDisplayName = _localProfile.DisplayName,
                Body = body,
                FileTransferId = transferId,
                AttachmentFileName = fileName,
                AttachmentSizeBytes = sizeBytes,
                SentAt = sentAt.ToString("o")
            };

            bool sent = false;

            // Attempt 1: SignalR WebSocket invocation
            try
            {
                var hub = await GetOrCreateConnectionAsync(targetPeer.IpAddress, targetPeer.HttpPort, cancellationToken);
                if (hub.State == HubConnectionState.Connected)
                {
                    await hub.InvokeAsync("SendDirectMessage", payload, cancellationToken);
                    sent = true;
                }
            }
            catch
            {
                // Fallback to HTTP REST endpoint
            }

            // Attempt 2: Direct HTTP POST fallback
            if (!sent)
            {
                var httpUrl = $"http://{targetPeer.IpAddress}:{targetPeer.HttpPort}/api/chat/message";
                var resp = await _httpClient.PostAsJsonAsync(httpUrl, payload, cancellationToken);
                if (resp.IsSuccessStatusCode)
                {
                    sent = true;
                }
                else
                {
                    return Result<Message>.Failure($"Failed to deliver message to peer: {resp.StatusCode}");
                }
            }

            var message = new Message
            {
                Id = msgId,
                ConversationId = conversationId,
                SenderDeviceId = _localProfile.DeviceId,
                SenderDisplayName = _localProfile.DisplayName,
                Body = body,
                FileTransferId = transferId,
                AttachmentFileName = fileName,
                AttachmentSizeBytes = sizeBytes,
                SentAt = sentAt,
                DeliveredAt = sentAt,
                IsSentByMe = true
            };

            await _messageRepo.SaveMessageAsync(message);
            return Result<Message>.Success(message);
        }
        catch (Exception ex)
        {
            return Result<Message>.Failure($"Failed to send message: {ex.Message}");
        }
    }

    public async Task<Result<Message>> SendGroupMessageAsync(Group group, IEnumerable<Peer> onlineMembers, string body, string? attachmentPath = null, CancellationToken cancellationToken = default)
    {
        var msgId = Guid.NewGuid().ToString("N");
        var sentAt = DateTime.UtcNow;

        string? transferId = null;
        string? fileName = null;
        long sizeBytes = 0;

        foreach (var memberPeer in onlineMembers)
        {
            if (memberPeer.DeviceId == _localProfile.DeviceId) continue;

            try
            {
                if (!string.IsNullOrWhiteSpace(attachmentPath) && File.Exists(attachmentPath) && transferId == null)
                {
                    var sendRes = await _transferService.SendFileAsync(memberPeer, attachmentPath, msgId, cancellationToken);
                    if (sendRes.IsSuccess && sendRes.Value != null)
                    {
                        transferId = sendRes.Value.Id;
                        fileName = sendRes.Value.FileName;
                        sizeBytes = sendRes.Value.SizeBytes;
                    }
                }

                var payload = new ChatMessagePayload
                {
                    MessageId = msgId,
                    GroupId = group.Id,
                    SenderDeviceId = _localProfile.DeviceId,
                    SenderDisplayName = _localProfile.DisplayName,
                    Body = body,
                    FileTransferId = transferId,
                    AttachmentFileName = fileName,
                    AttachmentSizeBytes = sizeBytes,
                    SentAt = sentAt.ToString("o")
                };

                bool sent = false;
                try
                {
                    var hub = await GetOrCreateConnectionAsync(memberPeer.IpAddress, memberPeer.HttpPort, cancellationToken);
                    if (hub.State == HubConnectionState.Connected)
                    {
                        await hub.InvokeAsync("SendGroupMessage", payload, cancellationToken);
                        sent = true;
                    }
                }
                catch { }

                if (!sent)
                {
                    var httpUrl = $"http://{memberPeer.IpAddress}:{memberPeer.HttpPort}/api/chat/group";
                    await _httpClient.PostAsJsonAsync(httpUrl, payload, cancellationToken);
                }
            }
            catch
            {
                // Continue fanout to other online members
            }
        }

        var message = new Message
        {
            Id = msgId,
            ConversationId = group.Id,
            SenderDeviceId = _localProfile.DeviceId,
            SenderDisplayName = _localProfile.DisplayName,
            Body = body,
            FileTransferId = transferId,
            AttachmentFileName = fileName,
            AttachmentSizeBytes = sizeBytes,
            SentAt = sentAt,
            DeliveredAt = sentAt,
            IsSentByMe = true
        };

        await _messageRepo.SaveMessageAsync(message);
        return Result<Message>.Success(message);
    }

    public async Task SendTypingNotificationAsync(Peer targetPeer)
    {
        try
        {
            bool sent = false;
            try
            {
                var hub = await GetOrCreateConnectionAsync(targetPeer.IpAddress, targetPeer.HttpPort);
                if (hub.State == HubConnectionState.Connected)
                {
                    await hub.InvokeAsync("SendTyping", _localProfile.DeviceId);
                    sent = true;
                }
            }
            catch { }

            if (!sent)
            {
                var httpUrl = $"http://{targetPeer.IpAddress}:{targetPeer.HttpPort}/api/chat/typing";
                var req = new TypingNotificationRequest { SenderDeviceId = _localProfile.DeviceId };
                await _httpClient.PostAsJsonAsync(httpUrl, req);
            }
        }
        catch { }
    }

    public async Task<Result> ReceiveDirectMessageAsync(ChatMessagePayload payload)
    {
        try
        {
            var sentAt = DateTime.TryParse(payload.SentAt, out DateTime dt) ? dt : DateTime.UtcNow;
            var msg = new Message
            {
                Id = payload.MessageId,
                ConversationId = payload.SenderDeviceId,
                SenderDeviceId = payload.SenderDeviceId,
                SenderDisplayName = payload.SenderDisplayName,
                Body = payload.Body,
                FileTransferId = payload.FileTransferId,
                AttachmentFileName = payload.AttachmentFileName,
                AttachmentSizeBytes = payload.AttachmentSizeBytes,
                SentAt = sentAt,
                DeliveredAt = DateTime.UtcNow,
                IsSentByMe = false
            };

            await _messageRepo.SaveMessageAsync(msg);
            MessageReceived?.Invoke(this, msg);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error processing received message: {ex.Message}");
        }
    }

    public async Task<Result> ReceiveGroupMessageAsync(ChatMessagePayload payload)
    {
        try
        {
            var sentAt = DateTime.TryParse(payload.SentAt, out DateTime dt) ? dt : DateTime.UtcNow;
            var msg = new Message
            {
                Id = payload.MessageId,
                ConversationId = payload.GroupId,
                SenderDeviceId = payload.SenderDeviceId,
                SenderDisplayName = payload.SenderDisplayName,
                Body = payload.Body,
                FileTransferId = payload.FileTransferId,
                AttachmentFileName = payload.AttachmentFileName,
                AttachmentSizeBytes = payload.AttachmentSizeBytes,
                SentAt = sentAt,
                DeliveredAt = DateTime.UtcNow,
                IsSentByMe = false
            };

            await _messageRepo.SaveMessageAsync(msg);
            MessageReceived?.Invoke(this, msg);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error processing received group message: {ex.Message}");
        }
    }

    public Task ReceiveTypingAsync(string senderDeviceId)
    {
        TypingIndicatorReceived?.Invoke(this, senderDeviceId);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Conversation>> GetConversationsAsync() => await _messageRepo.GetConversationsAsync();

    public async Task<IReadOnlyList<Message>> GetMessagesAsync(string conversationId, int limit = 50) => await _messageRepo.GetMessagesAsync(conversationId, limit);

    private async Task<HubConnection> GetOrCreateConnectionAsync(string ip, int port, CancellationToken ct = default)
    {
        var key = $"{ip}:{port}";
        if (_hubConnections.TryGetValue(key, out var existingHub))
        {
            if (existingHub.State == HubConnectionState.Connected)
            {
                return existingHub;
            }
            try
            {
                await existingHub.DisposeAsync();
            }
            catch { }
            _hubConnections.TryRemove(key, out _);
        }

        var url = $"http://{ip}:{port}/hub/chat";
        var hub = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .Build();

        hub.On<ChatMessagePayload>("ReceiveMessage", async (payload) =>
        {
            await ReceiveDirectMessageAsync(payload);
        });

        hub.On<ChatMessagePayload>("ReceiveGroupMessage", async (payload) =>
        {
            await ReceiveGroupMessageAsync(payload);
        });

        hub.On<string>("ReceiveTyping", (senderId) =>
        {
            ReceiveTypingAsync(senderId);
        });

        await hub.StartAsync(ct);
        _hubConnections[key] = hub;
        return hub;
    }
}
