using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using LocalShare.Common;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;

namespace LocalShare.Networking.Transfer;

public class InitiateTransferRequest
{
    public string TransferId { get; set; } = string.Empty;
    public string SenderDeviceId { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; } = 0;
    public string Sha256 { get; set; } = string.Empty;
    public string? ChatMessageId { get; set; }
}

public class TransferService : ITransferService
{
    private readonly Profile _localProfile;
    private readonly ITransferRepository _transferRepo;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, TransferItem> _activeTransfers = new();
    private readonly ConcurrentDictionary<string, FileStream> _incomingStreams = new();

    public event EventHandler<TransferItem>? TransferProgressChanged;
    public event EventHandler<TransferItem>? FileReceived;

    public TransferService(Profile localProfile, ITransferRepository transferRepo, HttpClient? httpClient = null)
    {
        _localProfile = localProfile;
        _transferRepo = transferRepo;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<Result<TransferItem>> SendFileAsync(Peer targetPeer, string filePath, string? chatMessageId = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return Result<TransferItem>.Failure("Source file does not exist.");

        var fileInfo = new FileInfo(filePath);
        var transferId = Guid.NewGuid().ToString("N");
        var sha256 = $"{fileInfo.Length:X}-{fileInfo.LastWriteTimeUtc.Ticks:X}";

        var transfer = new TransferItem
        {
            Id = transferId,
            Direction = TransferDirection.Outgoing,
            PeerDeviceId = targetPeer.DeviceId,
            PeerDisplayName = targetPeer.DisplayName,
            FileName = fileInfo.Name,
            SizeBytes = fileInfo.Length,
            BytesTransferred = 0,
            Sha256 = sha256,
            Status = TransferStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            FilePath = filePath,
            ChatMessageId = chatMessageId
        };

        _activeTransfers[transferId] = transfer;
        await _transferRepo.SaveTransferAsync(transfer);
        TransferProgressChanged?.Invoke(this, transfer);

        try
        {
            // 1. Initiate transfer endpoint call
            var initUrl = $"http://{targetPeer.IpAddress}:{targetPeer.HttpPort}/api/transfer/initiate";
            var initReq = new InitiateTransferRequest
            {
                TransferId = transferId,
                SenderDeviceId = _localProfile.DeviceId,
                SenderDisplayName = _localProfile.DisplayName,
                FileName = fileInfo.Name,
                SizeBytes = fileInfo.Length,
                Sha256 = sha256,
                ChatMessageId = chatMessageId
            };

            var resp = await _httpClient.PostAsJsonAsync(initUrl, initReq, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                transfer.Status = TransferStatus.Failed;
                await _transferRepo.UpdateTransferStatusAsync(transferId, TransferStatus.Failed, 0);
                TransferProgressChanged?.Invoke(this, transfer);
                return Result<TransferItem>.Failure($"Peer rejected transfer: {resp.StatusCode}");
            }

            // 2. Stream file in chunks (256 KB)
            const int chunkSize = 256 * 1024;
            using var fileStream = File.OpenRead(filePath);
            var buffer = new byte[chunkSize];
            int bytesRead;
            long totalSent = 0;

            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                if (transfer.Status == TransferStatus.Paused || transfer.Status == TransferStatus.Cancelled)
                {
                    break;
                }

                var chunkUrl = $"http://{targetPeer.IpAddress}:{targetPeer.HttpPort}/api/transfer/{transferId}/chunk?offset={totalSent}";
                using var content = new ByteArrayContent(buffer, 0, bytesRead);
                var chunkResp = await _httpClient.PostAsync(chunkUrl, content, cancellationToken);

                if (!chunkResp.IsSuccessStatusCode)
                {
                    transfer.Status = TransferStatus.Failed;
                    await _transferRepo.UpdateTransferStatusAsync(transferId, TransferStatus.Failed, totalSent);
                    TransferProgressChanged?.Invoke(this, transfer);
                    return Result<TransferItem>.Failure("Chunk upload failed.");
                }

                totalSent += bytesRead;
                transfer.BytesTransferred = totalSent;
                TransferProgressChanged?.Invoke(this, transfer);
            }

            if (transfer.Status == TransferStatus.Cancelled)
            {
                await _transferRepo.UpdateTransferStatusAsync(transferId, TransferStatus.Cancelled, totalSent);
                TransferProgressChanged?.Invoke(this, transfer);
                return Result<TransferItem>.Failure("Transfer cancelled by user.");
            }

            if (totalSent == fileInfo.Length)
            {
                transfer.Status = TransferStatus.Completed;
                transfer.CompletedAt = DateTime.UtcNow;
                await _transferRepo.UpdateTransferStatusAsync(transferId, TransferStatus.Completed, totalSent);
                TransferProgressChanged?.Invoke(this, transfer);
            }

            return Result<TransferItem>.Success(transfer);
        }
        catch (Exception ex)
        {
            if (transfer.Status != TransferStatus.Cancelled)
            {
                transfer.Status = TransferStatus.Failed;
                await _transferRepo.UpdateTransferStatusAsync(transferId, TransferStatus.Failed, transfer.BytesTransferred);
                TransferProgressChanged?.Invoke(this, transfer);
            }
            return Result<TransferItem>.Failure($"Transfer exception: {ex.Message}");
        }
    }

    public async Task<Result<TransferItem>> InitiateIncomingTransferAsync(string transferId, string senderDeviceId, string senderDisplayName, string fileName, long sizeBytes, string sha256, string? chatMessageId)
    {
        var sanitizedSenderName = string.Join("_", senderDisplayName.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(sanitizedSenderName)) sanitizedSenderName = "UnknownSender";

        var targetFolder = Path.Combine(_localProfile.ReceivedFilesRoot, sanitizedSenderName);
        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

        var savePath = Path.Combine(targetFolder, fileName);
        int collisionIndex = 1;
        while (File.Exists(savePath))
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            savePath = Path.Combine(targetFolder, $"{nameWithoutExt} ({collisionIndex}){ext}");
            collisionIndex++;
        }

        var transfer = new TransferItem
        {
            Id = transferId,
            Direction = TransferDirection.Incoming,
            PeerDeviceId = senderDeviceId,
            PeerDisplayName = senderDisplayName,
            FileName = fileName,
            SizeBytes = sizeBytes,
            BytesTransferred = 0,
            Sha256 = sha256,
            Status = TransferStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            FilePath = savePath,
            ChatMessageId = chatMessageId
        };

        _activeTransfers[transferId] = transfer;
        await _transferRepo.SaveTransferAsync(transfer);
        TransferProgressChanged?.Invoke(this, transfer);

        var stream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        _incomingStreams[transferId] = stream;

        return Result<TransferItem>.Success(transfer);
    }

    public async Task<Result> ReceiveChunkAsync(string transferId, long offset, byte[] chunkData, int count)
    {
        if (!_activeTransfers.TryGetValue(transferId, out var transfer) || !_incomingStreams.TryGetValue(transferId, out var stream))
        {
            return Result.Failure("Unknown or uninitialized transfer session.");
        }

        if (transfer.Status == TransferStatus.Cancelled)
        {
            return Result.Failure("Transfer has been cancelled.");
        }

        lock (stream)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            stream.Write(chunkData, 0, count);
            stream.Flush();
        }

        transfer.BytesTransferred += count;

        if (transfer.BytesTransferred >= transfer.SizeBytes)
        {
            stream.Close();
            stream.Dispose();
            _incomingStreams.TryRemove(transferId, out _);

            transfer.Status = TransferStatus.Completed;
            transfer.CompletedAt = DateTime.UtcNow;
            await _transferRepo.UpdateTransferStatusAsync(transferId, TransferStatus.Completed, transfer.BytesTransferred, transfer.FilePath);

            if (transfer.Direction == TransferDirection.Incoming)
            {
                FileReceived?.Invoke(this, transfer);
            }
        }

        TransferProgressChanged?.Invoke(this, transfer);
        return Result.Success();
    }

    public async Task<Result> PauseTransferAsync(string transferId)
    {
        if (_activeTransfers.TryGetValue(transferId, out var transfer))
        {
            transfer.Status = TransferStatus.Paused;
            await _transferRepo.UpdateTransferStatusAsync(transferId, TransferStatus.Paused, transfer.BytesTransferred);
            TransferProgressChanged?.Invoke(this, transfer);
            return Result.Success();
        }
        return Result.Failure("Transfer not found.");
    }

    public async Task<Result> ResumeTransferAsync(string transferId)
    {
        if (_activeTransfers.TryGetValue(transferId, out var transfer))
        {
            transfer.Status = TransferStatus.InProgress;
            await _transferRepo.UpdateTransferStatusAsync(transferId, TransferStatus.InProgress, transfer.BytesTransferred);
            TransferProgressChanged?.Invoke(this, transfer);
            return Result.Success();
        }
        return Result.Failure("Transfer not found.");
    }

    public async Task<Result> CancelTransferAsync(string transferId)
    {
        if (_activeTransfers.TryGetValue(transferId, out var transfer))
        {
            transfer.Status = TransferStatus.Cancelled;
            if (_incomingStreams.TryRemove(transferId, out var stream))
            {
                stream.Close();
                stream.Dispose();
            }
            await _transferRepo.UpdateTransferStatusAsync(transferId, TransferStatus.Cancelled, transfer.BytesTransferred);
            TransferProgressChanged?.Invoke(this, transfer);
            return Result.Success();
        }
        return Result.Failure("Transfer session not found.");
    }

    public async Task<IReadOnlyList<TransferItem>> GetTransferLogsAsync() => await _transferRepo.GetAllTransfersAsync();

    public async Task ClearAllTransferLogsAsync()
    {
        _activeTransfers.Clear();
        await _transferRepo.ClearAllTransfersAsync();
    }

    public TransferItem? GetTransfer(string transferId)
    {
        _activeTransfers.TryGetValue(transferId, out var item);
        return item;
    }
}
