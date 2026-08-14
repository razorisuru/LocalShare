using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using LocalShare.Common;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;
using LocalShare.Networking.Chat;
using LocalShare.Networking.PublicSpace;
using LocalShare.Networking.Transfer;

namespace LocalShare.Networking.Http;

public class KestrelServerHost
{
    private readonly Profile _localProfile;
    private readonly ITransferService _transferService;
    private readonly IPublicSpaceService _publicSpaceService;
    private readonly IChatService _chatService;
    private WebApplication? _app;

    public KestrelServerHost(
        Profile localProfile,
        ITransferService transferService,
        IPublicSpaceService publicSpaceService,
        IChatService chatService)
    {
        _localProfile = localProfile;
        _transferService = transferService;
        _publicSpaceService = publicSpaceService;
        _chatService = chatService;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseKestrel(options =>
        {
            options.ListenAnyIP(_localProfile.HttpPort);
        });

        builder.Services.AddSingleton(_chatService);
        builder.Services.AddSingleton(_transferService);
        builder.Services.AddSingleton(_publicSpaceService);
        builder.Services.AddSingleton(_localProfile);

        builder.Services.AddSignalR();
        builder.Services.AddRouting();

        _app = builder.Build();

        _app.UseRouting();

        // 1. GET /api/profile
        _app.MapGet("/api/profile", () => Results.Ok(_localProfile));

        // 2. POST /api/transfer/initiate
        _app.MapPost("/api/transfer/initiate", async (InitiateTransferRequest req) =>
        {
            var res = await _transferService.InitiateIncomingTransferAsync(
                req.TransferId, req.SenderDeviceId, req.SenderDisplayName, req.FileName, req.SizeBytes, req.Sha256, req.ChatMessageId);

            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(res.Error);
        });

        // 3. POST /api/transfer/{id}/chunk
        _app.MapPost("/api/transfer/{id}/chunk", async (string id, HttpRequest request) =>
        {
            long offset = 0;
            if (request.Query.TryGetValue("offset", out var offsetVal) && long.TryParse(offsetVal, out var parsedOffset))
            {
                offset = parsedOffset;
            }

            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms);
            var buffer = ms.ToArray();

            var res = await _transferService.ReceiveChunkAsync(id, offset, buffer, buffer.Length);
            return res.IsSuccess ? Results.Ok() : Results.BadRequest(res.Error);
        });

        // 4. GET /api/transfer/{id}/status
        _app.MapGet("/api/transfer/{id}/status", (string id) =>
        {
            var transfer = _transferService.GetTransfer(id);
            return transfer != null ? Results.Ok(transfer) : Results.NotFound();
        });

        // 5. GET /api/public/list
        _app.MapGet("/api/public/list", () =>
        {
            var files = _publicSpaceService.GetLocalSharedFiles();
            return Results.Ok(files);
        });

        // 6. GET /api/public/download/{fileId} (Supports HTTP Range headers)
        _app.MapGet("/api/public/download/{fileId}", (string fileId) =>
        {
            var files = _publicSpaceService.GetLocalSharedFiles();
            var target = files.FirstOrDefault(f => f.Id == fileId);
            if (target == null || string.IsNullOrWhiteSpace(_localProfile.PublicSpacePath))
                return Results.NotFound();

            var fullPath = Path.Combine(_localProfile.PublicSpacePath, target.RelativePath);
            if (!File.Exists(fullPath)) return Results.NotFound();

            return Results.File(fullPath, enableRangeProcessing: true, fileDownloadName: target.FileName);
        });

        // 7. WS /hub/chat
        _app.MapHub<ChatHub>("/hub/chat");

        // 8. POST /api/chat/message (Direct Chat REST Endpoint)
        _app.MapPost("/api/chat/message", async (ChatMessagePayload payload) =>
        {
            var res = await _chatService.ReceiveDirectMessageAsync(payload);
            return res.IsSuccess ? Results.Ok() : Results.BadRequest(res.Error);
        });

        // 9. POST /api/chat/group (Group Chat REST Endpoint)
        _app.MapPost("/api/chat/group", async (ChatMessagePayload payload) =>
        {
            var res = await _chatService.ReceiveGroupMessageAsync(payload);
            return res.IsSuccess ? Results.Ok() : Results.BadRequest(res.Error);
        });

        // 10. POST /api/chat/typing (Typing Notification REST Endpoint)
        _app.MapPost("/api/chat/typing", async (TypingNotificationRequest req) =>
        {
            await _chatService.ReceiveTypingAsync(req.SenderDeviceId);
            return Results.Ok();
        });

        await _app.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app != null)
        {
            await _app.StopAsync(cancellationToken);
            await _app.DisposeAsync();
        }
    }
}
