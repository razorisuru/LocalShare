namespace LocalShare.Core.Models;

public class UpdateInfo
{
    public string Version { get; set; } = "1.0.0";
    public string ReleaseDate { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string Changelog { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
}
