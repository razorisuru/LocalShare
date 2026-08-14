using Dapper;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;

namespace LocalShare.Data.Repositories;

public class SqliteRepositories : IProfileRepository, IPeerRepository, IMessageRepository, IGroupRepository, ITransferRepository
{
    private readonly DatabaseInitializer _db;

    public SqliteRepositories(DatabaseInitializer db)
    {
        _db = db;
    }

    #region IProfileRepository
    public async Task<Profile> GetProfileAsync()
    {
        using var conn = _db.CreateConnection();
        var profile = await conn.QueryFirstOrDefaultAsync<Profile>("SELECT * FROM Profiles LIMIT 1");
        if (profile == null)
        {
            profile = new Profile
            {
                ReceivedFilesRoot = LocalShare.Common.Constants.ReceivedFolder
            };
            await SaveProfileAsync(profile);
        }
        return profile;
    }

    public async Task SaveProfileAsync(Profile profile)
    {
        using var conn = _db.CreateConnection();
        var sql = @"
            INSERT OR REPLACE INTO Profiles (DeviceId, DisplayName, AvatarPath, AccentColor, PublicSpacePath, ReceivedFilesRoot, HttpPort, ProtocolVersion, AppVersion)
            VALUES (@DeviceId, @DisplayName, @AvatarPath, @AccentColor, @PublicSpacePath, @ReceivedFilesRoot, @HttpPort, @ProtocolVersion, @AppVersion);
        ";
        await conn.ExecuteAsync(sql, profile);
    }
    #endregion

    #region IPeerRepository
    public async Task UpsertPeerAsync(Peer peer)
    {
        using var conn = _db.CreateConnection();
        var sql = @"
            INSERT INTO Peers (DeviceId, DisplayName, AvatarHash, AccentColor, IpAddress, HttpPort, HasPublicSpace, LastSeenAt, ProtocolVersion, AppVersion)
            VALUES (@DeviceId, @DisplayName, @AvatarHash, @AccentColor, @IpAddress, @HttpPort, @HasPublicSpace, @LastSeenAtStr, @ProtocolVersion, @AppVersion)
            ON CONFLICT(DeviceId) DO UPDATE SET
                DisplayName = excluded.DisplayName,
                AvatarHash = excluded.AvatarHash,
                AccentColor = excluded.AccentColor,
                IpAddress = excluded.IpAddress,
                HttpPort = excluded.HttpPort,
                HasPublicSpace = excluded.HasPublicSpace,
                LastSeenAt = excluded.LastSeenAt,
                ProtocolVersion = excluded.ProtocolVersion,
                AppVersion = excluded.AppVersion;
        ";
        await conn.ExecuteAsync(sql, new
        {
            peer.DeviceId,
            peer.DisplayName,
            peer.AvatarHash,
            peer.AccentColor,
            peer.IpAddress,
            peer.HttpPort,
            HasPublicSpace = peer.HasPublicSpace ? 1 : 0,
            LastSeenAtStr = peer.LastSeenAt.ToString("o"),
            peer.ProtocolVersion,
            peer.AppVersion
        });
    }

    public async Task<IReadOnlyList<Peer>> GetAllPeersAsync()
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync("SELECT * FROM Peers");
        var list = new List<Peer>();
        foreach (var r in rows)
        {
            list.Add(new Peer
            {
                DeviceId = r.DeviceId,
                DisplayName = r.DisplayName,
                AvatarHash = r.AvatarHash ?? string.Empty,
                AccentColor = r.AccentColor ?? "#0078D4",
                IpAddress = r.IpAddress,
                HttpPort = (int)r.HttpPort,
                HasPublicSpace = r.HasPublicSpace == 1,
                LastSeenAt = DateTime.TryParse((string)r.LastSeenAt, out DateTime dt) ? dt : DateTime.UtcNow,
                ProtocolVersion = r.ProtocolVersion ?? "1.0.0",
                AppVersion = r.AppVersion ?? "1.0.0"
            });
        }
        return list;
    }
    #endregion

    #region IMessageRepository
    public async Task SaveMessageAsync(Message message)
    {
        using var conn = _db.CreateConnection();
        var sqlMsg = @"
            INSERT OR REPLACE INTO Messages (Id, ConversationId, SenderDeviceId, SenderDisplayName, Body, FileTransferId, AttachmentFileName, AttachmentSizeBytes, SentAt, DeliveredAt)
            VALUES (@Id, @ConversationId, @SenderDeviceId, @SenderDisplayName, @Body, @FileTransferId, @AttachmentFileName, @AttachmentSizeBytes, @SentAtStr, @DeliveredAtStr);
        ";
        await conn.ExecuteAsync(sqlMsg, new
        {
            message.Id,
            message.ConversationId,
            message.SenderDeviceId,
            message.SenderDisplayName,
            message.Body,
            message.FileTransferId,
            message.AttachmentFileName,
            message.AttachmentSizeBytes,
            SentAtStr = message.SentAt.ToString("o"),
            DeliveredAtStr = message.DeliveredAt?.ToString("o")
        });

        // Upsert Conversation so it is guaranteed to exist and display in conversations list
        var existingConv = await conn.QueryFirstOrDefaultAsync<Conversation>(
            "SELECT * FROM Conversations WHERE Id = @Id", new { Id = message.ConversationId });

        if (existingConv == null)
        {
            // Check if this is a group
            var grp = await conn.QueryFirstOrDefaultAsync("SELECT * FROM Groups WHERE Id = @Id", new { Id = message.ConversationId });
            if (grp != null)
            {
                var groupConvSql = @"
                    INSERT INTO Conversations (Id, Type, DisplayName, GroupId, LastMessageAt, UnreadCount)
                    VALUES (@Id, @Type, @DisplayName, @GroupId, @LastMessageAtStr, 0);
                ";
                await conn.ExecuteAsync(groupConvSql, new
                {
                    Id = message.ConversationId,
                    Type = (int)ConversationType.Group,
                    DisplayName = (string)grp.Name,
                    GroupId = message.ConversationId,
                    LastMessageAtStr = message.SentAt.ToString("o")
                });
            }
            else
            {
                // Direct conversation
                var peer = await conn.QueryFirstOrDefaultAsync("SELECT * FROM Peers WHERE DeviceId = @DeviceId", new { DeviceId = message.ConversationId });
                string convDisplayName;
                if (peer != null)
                {
                    convDisplayName = (string)peer.DisplayName;
                }
                else if (message.SenderDeviceId == message.ConversationId)
                {
                    // Incoming message: conversation Id is the sender's device Id
                    convDisplayName = message.SenderDisplayName;
                }
                else
                {
                    // Outgoing message: conversation Id is target peer's device Id
                    convDisplayName = message.ConversationId;
                }

                var directConvSql = @"
                    INSERT INTO Conversations (Id, Type, DisplayName, TargetDeviceId, LastMessageAt, UnreadCount)
                    VALUES (@Id, @Type, @DisplayName, @TargetDeviceId, @LastMessageAtStr, 0);
                ";
                await conn.ExecuteAsync(directConvSql, new
                {
                    Id = message.ConversationId,
                    Type = (int)ConversationType.Direct,
                    DisplayName = convDisplayName,
                    TargetDeviceId = message.ConversationId,
                    LastMessageAtStr = message.SentAt.ToString("o")
                });
            }
        }
        else
        {
            // Update timestamp
            // If it's a direct conversation and the message is incoming from peer, update DisplayName to reflect sender's latest display name
            if (existingConv.Type == ConversationType.Direct && message.SenderDeviceId == message.ConversationId && !string.IsNullOrWhiteSpace(message.SenderDisplayName))
            {
                await conn.ExecuteAsync(@"
                    UPDATE Conversations 
                    SET DisplayName = @DisplayName, LastMessageAt = @LastMessageAtStr 
                    WHERE Id = @Id;
                ", new
                {
                    Id = message.ConversationId,
                    DisplayName = message.SenderDisplayName,
                    LastMessageAtStr = message.SentAt.ToString("o")
                });
            }
            else
            {
                await conn.ExecuteAsync(@"
                    UPDATE Conversations 
                    SET LastMessageAt = @LastMessageAtStr 
                    WHERE Id = @Id;
                ", new
                {
                    Id = message.ConversationId,
                    LastMessageAtStr = message.SentAt.ToString("o")
                });
            }
        }
    }

    public async Task<IReadOnlyList<Message>> GetMessagesAsync(string conversationId, int limit = 50)
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync(
            "SELECT * FROM Messages WHERE ConversationId = @ConversationId ORDER BY SentAt ASC LIMIT @Limit",
            new { ConversationId = conversationId, Limit = limit });

        var list = new List<Message>();
        foreach (var r in rows)
        {
            list.Add(new Message
            {
                Id = r.Id,
                ConversationId = r.ConversationId,
                SenderDeviceId = r.SenderDeviceId,
                SenderDisplayName = r.SenderDisplayName,
                Body = r.Body ?? string.Empty,
                FileTransferId = r.FileTransferId,
                AttachmentFileName = r.AttachmentFileName,
                AttachmentSizeBytes = (long)(r.AttachmentSizeBytes ?? 0),
                SentAt = DateTime.TryParse((string)r.SentAt, out DateTime st) ? st : DateTime.UtcNow,
                DeliveredAt = DateTime.TryParse((string)r.DeliveredAt, out DateTime dt) ? dt : null
            });
        }
        return list;
    }

    public async Task<IReadOnlyList<Conversation>> GetConversationsAsync()
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync("SELECT * FROM Conversations ORDER BY LastMessageAt DESC");
        var list = new List<Conversation>();
        foreach (var r in rows)
        {
            list.Add(new Conversation
            {
                Id = r.Id,
                Type = (ConversationType)r.Type,
                DisplayName = r.DisplayName,
                TargetDeviceId = r.TargetDeviceId,
                GroupId = r.GroupId,
                LastMessageAt = DateTime.TryParse((string)r.LastMessageAt, out DateTime dt) ? dt : DateTime.UtcNow,
                UnreadCount = (int)(r.UnreadCount ?? 0)
            });
        }
        return list;
    }

    public async Task MarkAsReadAsync(string conversationId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("UPDATE Conversations SET UnreadCount = 0 WHERE Id = @Id", new { Id = conversationId });
    }
    #endregion

    #region IGroupRepository
    public async Task SaveGroupAsync(Group group)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "INSERT OR REPLACE INTO Groups (Id, Name, CreatedByDeviceId, CreatedAt) VALUES (@Id, @Name, @CreatedByDeviceId, @CreatedAtStr);",
            new { group.Id, group.Name, group.CreatedByDeviceId, CreatedAtStr = group.CreatedAt.ToString("o") });

        foreach (var member in group.Members)
        {
            await AddMemberAsync(group.Id, member);
        }
    }

    public async Task<Group?> GetGroupByIdAsync(string groupId)
    {
        using var conn = _db.CreateConnection();
        var g = await conn.QueryFirstOrDefaultAsync("SELECT * FROM Groups WHERE Id = @Id", new { Id = groupId });
        if (g == null) return null;

        var members = await conn.QueryAsync("SELECT * FROM GroupMembers WHERE GroupId = @GroupId", new { GroupId = groupId });
        var memberList = new List<GroupMember>();
        foreach (var m in members)
        {
            memberList.Add(new GroupMember
            {
                GroupId = m.GroupId,
                DeviceId = m.DeviceId,
                DisplayName = m.DisplayName,
                JoinedAt = DateTime.TryParse((string)m.JoinedAt, out DateTime dt) ? dt : DateTime.UtcNow
            });
        }

        return new Group
        {
            Id = g.Id,
            Name = g.Name,
            CreatedByDeviceId = g.CreatedByDeviceId,
            CreatedAt = DateTime.TryParse((string)g.CreatedAt, out DateTime cdt) ? cdt : DateTime.UtcNow,
            Members = memberList
        };
    }

    public async Task<IReadOnlyList<Group>> GetAllGroupsAsync()
    {
        using var conn = _db.CreateConnection();
        var groups = await conn.QueryAsync("SELECT * FROM Groups ORDER BY CreatedAt DESC");
        var list = new List<Group>();
        foreach (var g in groups)
        {
            var grp = await GetGroupByIdAsync((string)g.Id);
            if (grp != null) list.Add(grp);
        }
        return list;
    }

    public async Task AddMemberAsync(string groupId, GroupMember member)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO GroupMembers (GroupId, DeviceId, DisplayName, JoinedAt)
            VALUES (@GroupId, @DeviceId, @DisplayName, @JoinedAtStr);
        ", new { GroupId = groupId, member.DeviceId, member.DisplayName, JoinedAtStr = member.JoinedAt.ToString("o") });
    }

    public async Task RemoveMemberAsync(string groupId, string deviceId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM GroupMembers WHERE GroupId = @GroupId AND DeviceId = @DeviceId", new { GroupId = groupId, DeviceId = deviceId });
    }
    #endregion

    #region ITransferRepository
    public async Task SaveTransferAsync(TransferItem transfer)
    {
        using var conn = _db.CreateConnection();
        var sql = @"
            INSERT OR REPLACE INTO Transfers (Id, Direction, PeerDeviceId, PeerDisplayName, FileName, SizeBytes, BytesTransferred, Sha256, Status, StartedAt, CompletedAt, FilePath, ChatMessageId)
            VALUES (@Id, @DirectionInt, @PeerDeviceId, @PeerDisplayName, @FileName, @SizeBytes, @BytesTransferred, @Sha256, @StatusInt, @StartedAtStr, @CompletedAtStr, @FilePath, @ChatMessageId);
        ";
        await conn.ExecuteAsync(sql, new
        {
            transfer.Id,
            DirectionInt = (int)transfer.Direction,
            transfer.PeerDeviceId,
            transfer.PeerDisplayName,
            transfer.FileName,
            transfer.SizeBytes,
            transfer.BytesTransferred,
            transfer.Sha256,
            StatusInt = (int)transfer.Status,
            StartedAtStr = transfer.StartedAt.ToString("o"),
            CompletedAtStr = transfer.CompletedAt?.ToString("o"),
            transfer.FilePath,
            transfer.ChatMessageId
        });
    }

    public async Task UpdateTransferStatusAsync(string id, TransferStatus status, long bytesTransferred, string? filePath = null)
    {
        using var conn = _db.CreateConnection();
        var completedAt = status == TransferStatus.Completed ? DateTime.UtcNow.ToString("o") : null;
        var sql = @"
            UPDATE Transfers SET Status = @StatusInt, BytesTransferred = @BytesTransferred, CompletedAt = COALESCE(@CompletedAtStr, CompletedAt), FilePath = COALESCE(@FilePath, FilePath)
            WHERE Id = @Id;
        ";
        await conn.ExecuteAsync(sql, new
        {
            Id = id,
            StatusInt = (int)status,
            BytesTransferred = bytesTransferred,
            CompletedAtStr = completedAt,
            FilePath = filePath
        });
    }

    public async Task<IReadOnlyList<TransferItem>> GetAllTransfersAsync()
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync("SELECT * FROM Transfers ORDER BY StartedAt DESC");
        var list = new List<TransferItem>();
        foreach (var r in rows)
        {
            list.Add(new TransferItem
            {
                Id = r.Id,
                Direction = (TransferDirection)r.Direction,
                PeerDeviceId = r.PeerDeviceId,
                PeerDisplayName = r.PeerDisplayName,
                FileName = r.FileName,
                SizeBytes = (long)r.SizeBytes,
                BytesTransferred = (long)r.BytesTransferred,
                Sha256 = r.Sha256 ?? string.Empty,
                Status = (TransferStatus)r.Status,
                StartedAt = DateTime.TryParse((string)r.StartedAt, out DateTime st) ? st : DateTime.UtcNow,
                CompletedAt = DateTime.TryParse((string)r.CompletedAt, out DateTime ct) ? ct : null,
                FilePath = r.FilePath ?? string.Empty,
                ChatMessageId = r.ChatMessageId
            });
        }
        return list;
    }

    public async Task<TransferItem?> GetTransferByIdAsync(string id)
    {
        using var conn = _db.CreateConnection();
        var r = await conn.QueryFirstOrDefaultAsync("SELECT * FROM Transfers WHERE Id = @Id", new { Id = id });
        if (r == null) return null;

        return new TransferItem
        {
            Id = r.Id,
            Direction = (TransferDirection)r.Direction,
            PeerDeviceId = r.PeerDeviceId,
            PeerDisplayName = r.PeerDisplayName,
            FileName = r.FileName,
            SizeBytes = (long)r.SizeBytes,
            BytesTransferred = (long)r.BytesTransferred,
            Sha256 = r.Sha256 ?? string.Empty,
            Status = (TransferStatus)r.Status,
            StartedAt = DateTime.TryParse((string)r.StartedAt, out DateTime st) ? st : DateTime.UtcNow,
            CompletedAt = DateTime.TryParse((string)r.CompletedAt, out DateTime ct) ? ct : null,
            FilePath = r.FilePath ?? string.Empty,
            ChatMessageId = r.ChatMessageId
        };
    }

    public async Task ClearAllTransfersAsync()
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Transfers;");
    }
    #endregion
}
