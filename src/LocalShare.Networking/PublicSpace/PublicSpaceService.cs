using System.Net.Http.Json;
using LocalShare.Common;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;

namespace LocalShare.Networking.PublicSpace;

public class PublicSpaceService : IPublicSpaceService
{
    private readonly Profile _localProfile;
    private readonly HttpClient _httpClient;
    private string? _publicFolderPath;
    private FileSystemWatcher? _watcher;

    public PublicSpaceService(Profile localProfile, HttpClient? httpClient = null)
    {
        _localProfile = localProfile;
        _httpClient = httpClient ?? new HttpClient();
        _publicFolderPath = localProfile.PublicSpacePath;

        if (!string.IsNullOrWhiteSpace(_publicFolderPath) && Directory.Exists(_publicFolderPath))
        {
            SetupWatcher(_publicFolderPath);
        }
    }

    public void SetPublicFolder(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            _publicFolderPath = folderPath;
            _localProfile.PublicSpacePath = folderPath;
            SetupWatcher(folderPath);
        }
    }

    public IReadOnlyList<PublicShareEntry> GetLocalSharedFiles()
    {
        if (string.IsNullOrWhiteSpace(_publicFolderPath) || !Directory.Exists(_publicFolderPath))
            return Array.Empty<PublicShareEntry>();

        var entries = new List<PublicShareEntry>();
        var rootDir = new DirectoryInfo(_publicFolderPath);

        foreach (var file in rootDir.GetFiles("*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(_publicFolderPath, file.FullName);
            entries.Add(new PublicShareEntry
            {
                Id = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(relativePath)),
                FileName = file.Name,
                RelativePath = relativePath,
                SizeBytes = file.Length,
                LastModified = file.LastWriteTimeUtc,
                IsDirectory = false
            });
        }

        return entries;
    }

    public async Task<Result<IReadOnlyList<PublicShareEntry>>> FetchRemotePublicFilesAsync(Peer peer, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"http://{peer.IpAddress}:{peer.HttpPort}/api/public/list";
            var result = await _httpClient.GetFromJsonAsync<List<PublicShareEntry>>(url, cancellationToken);
            return Result<IReadOnlyList<PublicShareEntry>>.Success(result ?? new List<PublicShareEntry>());
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<PublicShareEntry>>.Failure($"Failed to fetch remote public files: {ex.Message}");
        }
    }

    public async Task<Result<string>> DownloadPublicFileAsync(Peer peer, PublicShareEntry remoteFile, string destinationFolder, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            var savePath = Path.Combine(destinationFolder, remoteFile.FileName);
            var url = $"http://{peer.IpAddress}:{peer.HttpPort}/api/public/download/{remoteFile.Id}";

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Result<string>.Failure($"HTTP error: {response.StatusCode}");

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = File.Create(savePath);
            await stream.CopyToAsync(fileStream, cancellationToken);

            return Result<string>.Success(savePath);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Download failed: {ex.Message}");
        }
    }

    private void SetupWatcher(string folderPath)
    {
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(folderPath)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
    }
}
