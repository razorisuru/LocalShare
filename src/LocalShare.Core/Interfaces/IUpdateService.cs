using LocalShare.Common;
using LocalShare.Core.Models;

namespace LocalShare.Core.Interfaces;

public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<Result<UpdateInfo?>> CheckForUpdatesAsync(string? updateManifestUrl = null, CancellationToken cancellationToken = default);
    Task<Result> DownloadAndApplyUpdateAsync(UpdateInfo updateInfo, Action<double>? progressCallback = null, CancellationToken cancellationToken = default);
}
