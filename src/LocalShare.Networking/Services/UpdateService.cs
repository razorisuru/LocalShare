using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using LocalShare.Common;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;

namespace LocalShare.Networking.Services;

public class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    public const string DefaultUpdateManifestUrl = "https://raw.githubusercontent.com/razorisuru/LocalShare/main/dist/latest_version.json";

    public UpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public string CurrentVersion => AppVersionInfo.Version;

    public async Task<Result<UpdateInfo?>> CheckForUpdatesAsync(string? updateManifestUrl = null, CancellationToken cancellationToken = default)
    {
        var targetUrl = string.IsNullOrWhiteSpace(updateManifestUrl) ? DefaultUpdateManifestUrl : updateManifestUrl;

        try
        {
            var response = await _httpClient.GetAsync(targetUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<UpdateInfo?>.Failure($"Unable to reach update manifest at {targetUrl} (HTTP {response.StatusCode}).");
            }

            var updateInfo = await response.Content.ReadFromJsonAsync<UpdateInfo>(cancellationToken: cancellationToken);
            if (updateInfo == null || string.IsNullOrWhiteSpace(updateInfo.Version))
            {
                return Result<UpdateInfo?>.Failure("Invalid update manifest format.");
            }

            if (TryParseVersion(updateInfo.Version, out var remoteVer) && TryParseVersion(CurrentVersion, out var currentVer))
            {
                if (remoteVer > currentVer)
                {
                    return Result<UpdateInfo?>.Success(updateInfo);
                }
            }

            return Result<UpdateInfo?>.Success(null); // Already up to date
        }
        catch (Exception ex)
        {
            return Result<UpdateInfo?>.Failure($"Update check error: {ex.Message}");
        }
    }

    public async Task<Result> DownloadAndApplyUpdateAsync(UpdateInfo updateInfo, Action<double>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
        {
            return Result.Failure("Download URL is missing in update info.");
        }

        try
        {
            // Support local file path for offline testing
            if (File.Exists(updateInfo.DownloadUrl) || updateInfo.DownloadUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var localPath = updateInfo.DownloadUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(updateInfo.DownloadUrl).LocalPath
                    : updateInfo.DownloadUrl;

                if (!File.Exists(localPath))
                {
                    return Result.Failure($"Local update installer file not found at: {localPath}");
                }

                var localStartInfo = new ProcessStartInfo
                {
                    FileName = localPath,
                    Arguments = "/SILENT /NORESTART",
                    UseShellExecute = true
                };
                Process.Start(localStartInfo);
                Environment.Exit(0);
                return Result.Success();
            }

            var tempInstallerPath = Path.Combine(Path.GetTempPath(), $"LocalShare_Setup_v{updateInfo.Version}.exe");

            using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return Result.Failure($"Remote installer file for v{updateInfo.Version} was not found on GitHub Releases (HTTP 404).\n\nTo resolve this: Please upload 'LocalShare_Setup_v{updateInfo.Version}.exe' to your GitHub Release assets at:\nhttps://github.com/razorisuru/LocalShare/releases/tag/v{updateInfo.Version}");
                }

                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(tempInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        double progressPercentage = (double)totalBytesRead / totalBytes * 100.0;
                        progressCallback?.Invoke(progressPercentage);
                    }
                }
            }

            // Execute the installer silently
            var startInfo = new ProcessStartInfo
            {
                FileName = tempInstallerPath,
                Arguments = "/SILENT /NORESTART",
                UseShellExecute = true
            };

            Process.Start(startInfo);

            // Shutdown the current running application instance cleanly
            Environment.Exit(0);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Update error: {ex.Message}");
        }
    }

    private static bool TryParseVersion(string verString, out Version version)
    {
        var cleanVer = verString.TrimStart('v', 'V');
        return Version.TryParse(cleanVer, out version!);
    }
}
