using Microsoft.Data.Sqlite;
using Dapper;
using LocalShare.Common;

namespace LocalShare.Data;

public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string? dbPath = null)
    {
        var path = dbPath ?? Constants.DatabasePath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = $"Data Source={path}";
    }

    public SqliteConnection CreateConnection() => new(_connectionString);

    public async Task InitializeAsync()
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();

        var sql = @"
            CREATE TABLE IF NOT EXISTS Profiles (
                DeviceId TEXT PRIMARY KEY,
                DisplayName TEXT NOT NULL,
                AvatarPath TEXT,
                AccentColor TEXT,
                PublicSpacePath TEXT,
                ReceivedFilesRoot TEXT,
                HttpPort INTEGER NOT NULL,
                ProtocolVersion TEXT,
                AppVersion TEXT
            );

            CREATE TABLE IF NOT EXISTS Peers (
                DeviceId TEXT PRIMARY KEY,
                DisplayName TEXT NOT NULL,
                AvatarHash TEXT,
                AccentColor TEXT,
                IpAddress TEXT NOT NULL,
                HttpPort INTEGER NOT NULL,
                HasPublicSpace INTEGER NOT NULL,
                LastSeenAt TEXT NOT NULL,
                ProtocolVersion TEXT,
                AppVersion TEXT
            );

            CREATE TABLE IF NOT EXISTS Conversations (
                Id TEXT PRIMARY KEY,
                Type INTEGER NOT NULL,
                DisplayName TEXT NOT NULL,
                TargetDeviceId TEXT,
                GroupId TEXT,
                LastMessageAt TEXT NOT NULL,
                UnreadCount INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Messages (
                Id TEXT PRIMARY KEY,
                ConversationId TEXT NOT NULL,
                SenderDeviceId TEXT NOT NULL,
                SenderDisplayName TEXT NOT NULL,
                Body TEXT,
                FileTransferId TEXT,
                AttachmentFileName TEXT,
                AttachmentSizeBytes INTEGER NOT NULL DEFAULT 0,
                SentAt TEXT NOT NULL,
                DeliveredAt TEXT
            );

            CREATE TABLE IF NOT EXISTS Groups (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                CreatedByDeviceId TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS GroupMembers (
                GroupId TEXT NOT NULL,
                DeviceId TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                JoinedAt TEXT NOT NULL,
                PRIMARY KEY (GroupId, DeviceId)
            );

            CREATE TABLE IF NOT EXISTS Transfers (
                Id TEXT PRIMARY KEY,
                Direction INTEGER NOT NULL,
                PeerDeviceId TEXT NOT NULL,
                PeerDisplayName TEXT NOT NULL,
                FileName TEXT NOT NULL,
                SizeBytes INTEGER NOT NULL,
                BytesTransferred INTEGER NOT NULL,
                Sha256 TEXT,
                Status INTEGER NOT NULL,
                StartedAt TEXT NOT NULL,
                CompletedAt TEXT,
                FilePath TEXT,
                ChatMessageId TEXT
            );
        ";

        await connection.ExecuteAsync(sql);
    }
}
