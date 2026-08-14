using Microsoft.AspNetCore.SignalR;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;

namespace LocalShare.Networking.Chat;

public interface IChatClient
{
    Task ReceiveMessage(ChatMessagePayload payload);
    Task ReceiveGroupMessage(ChatMessagePayload payload);
    Task ReceiveTyping(string senderDeviceId);
}

public class ChatHub : Hub<IChatClient>
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    public async Task SendDirectMessage(ChatMessagePayload payload)
    {
        await _chatService.ReceiveDirectMessageAsync(payload);
        await Clients.Others.ReceiveMessage(payload);
    }

    public async Task SendGroupMessage(ChatMessagePayload payload)
    {
        await _chatService.ReceiveGroupMessageAsync(payload);
        await Clients.Others.ReceiveGroupMessage(payload);
    }

    public async Task SendTyping(string senderDeviceId)
    {
        await _chatService.ReceiveTypingAsync(senderDeviceId);
        await Clients.Others.ReceiveTyping(senderDeviceId);
    }
}

