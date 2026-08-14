namespace LocalShare.Core.Models;

public class StagedFile
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public string FormattedSize
    {
        get
        {
            if (SizeBytes < 1024) return $"{SizeBytes} B";
            if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
            if (SizeBytes < 1024 * 1024 * 1024) return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
            return $"{SizeBytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }

    public string FileIcon
    {
        get
        {
            var ext = System.IO.Path.GetExtension(FileName).ToLowerInvariant();
            return ext switch
            {
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" => "🖼️",
                ".pdf" or ".doc" or ".docx" or ".txt" or ".rtf" or ".md" => "📄",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" => "🎥",
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => "🎵",
                ".exe" or ".msi" or ".bat" or ".ps1" => "⚙️",
                _ => "📁"
            };
        }
    }
}
