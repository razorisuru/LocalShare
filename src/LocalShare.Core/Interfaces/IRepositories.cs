using LocalShare.Core.Models;

namespace LocalShare.Core.Interfaces;

public interface IProfileRepository
{
    Task<Profile> GetProfileAsync();
    Task SaveProfileAsync(Profile profile);
}

public interface IPeerRepository
{
    Task UpsertPeerAsync(Peer peer);
    Task<IReadOnlyList<Peer>> GetAllPeersAsync();
}

public interface IMessageRepository
{
    Task SaveMessageAsync(Message message);
    Task<IReadOnlyList<Message>> GetMessagesAsync(string conversationId, int limit = 50);
    Task<IReadOnlyList<Conversation>> GetConversationsAsync();
    Task MarkAsReadAsync(string conversationId);
}

public interface IGroupRepository
{
    Task SaveGroupAsync(Group group);
    Task<Group?> GetGroupByIdAsync(string groupId);
    Task<IReadOnlyList<Group>> GetAllGroupsAsync();
    Task AddMemberAsync(string groupId, GroupMember member);
    Task RemoveMemberAsync(string groupId, string deviceId);
}

public interface ITransferRepository
{
    Task SaveTransferAsync(TransferItem transfer);
    Task UpdateTransferStatusAsync(string id, TransferStatus status, long bytesTransferred, string? filePath = null);
    Task<IReadOnlyList<TransferItem>> GetAllTransfersAsync();
    Task<TransferItem?> GetTransferByIdAsync(string id);
    Task ClearAllTransfersAsync();
}
