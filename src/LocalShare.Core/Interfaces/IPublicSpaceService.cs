using LocalShare.Common;
using LocalShare.Core.Models;

namespace LocalShare.Core.Interfaces;

public interface IPublicSpaceService
{
    void SetPublicFolder(string folderPath);
    IReadOnlyList<PublicShareEntry> GetLocalSharedFiles();
    Task<Result<IReadOnlyList<PublicShareEntry>>> FetchRemotePublicFilesAsync(Peer peer, CancellationToken cancellationToken = default);
    Task<Result<string>> DownloadPublicFileAsync(Peer peer, PublicShareEntry remoteFile, string destinationFolder, CancellationToken cancellationToken = default);
}
