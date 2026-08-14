using LocalShare.Common;
using LocalShare.Core.Models;

namespace LocalShare.Core.Interfaces;

public interface ITransferService
{
    event EventHandler<TransferItem>? TransferProgressChanged;
    event EventHandler<TransferItem>? FileReceived;

    Task<Result<TransferItem>> SendFileAsync(Peer targetPeer, string filePath, string? chatMessageId = null, CancellationToken cancellationToken = default);
    Task<Result<TransferItem>> InitiateIncomingTransferAsync(string transferId, string senderDeviceId, string senderDisplayName, string fileName, long sizeBytes, string sha256, string? chatMessageId);
    Task<Result> ReceiveChunkAsync(string transferId, long offset, byte[] chunkData, int count);
    Task<Result> PauseTransferAsync(string transferId);
    Task<Result> ResumeTransferAsync(string transferId);
    Task<Result> CancelTransferAsync(string transferId);
    Task<IReadOnlyList<TransferItem>> GetTransferLogsAsync();
    Task ClearAllTransferLogsAsync();
    TransferItem? GetTransfer(string transferId);
}
