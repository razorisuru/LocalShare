using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LocalShare.App.Views;

public partial class NotificationToastWindow : Window
{
    private readonly string? _filePath;
    private readonly DispatcherTimer _autoCloseTimer;
    private static readonly MediaPlayer _mediaPlayer = new();

    public NotificationToastWindow(string title, string fileName, string? filePath = null)
    {
        InitializeComponent();
        _filePath = filePath;

        TxtTitle.Text = title;
        TxtFileName.Text = fileName;

        // Position window in bottom-right corner of primary screen above taskbar
        Left = SystemParameters.WorkArea.Right - Width - 20;
        Top = SystemParameters.WorkArea.Bottom - Height - 20;

        // Set up auto-close timer (6 seconds)
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(6)
        };
        _autoCloseTimer.Tick += (s, e) => CloseToast();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Play custom notification sound (fahhh.mp3) with fallback to System Asterisk
        PlayNotificationSound();

        // Trigger slide-in animation
        var storyboard = FindResource("SlideInAnimation") as Storyboard;
        storyboard?.Begin(this);

        _autoCloseTimer.Start();
    }

    private static void PlayNotificationSound()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var mp3Path = Path.Combine(baseDir, "Assets", "fahhh.mp3");

            if (!File.Exists(mp3Path))
            {
                mp3Path = Path.Combine(baseDir, "fahhh.mp3");
            }

            if (!File.Exists(mp3Path))
            {
                var projectPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "LocalShare.App", "Assets", "fahhh.mp3");
                if (File.Exists(projectPath)) mp3Path = projectPath;
            }

            if (File.Exists(mp3Path))
            {
                _mediaPlayer.Open(new Uri(mp3Path, UriKind.Absolute));
                _mediaPlayer.Volume = 1.0; // 100% Maximum Volume
                _mediaPlayer.Play();
            }
            else
            {
                SystemSounds.Asterisk.Play();
            }
        }
        catch
        {
            SystemSounds.Asterisk.Play();
        }
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_filePath) && File.Exists(_filePath))
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{_filePath}\"");
            }
            catch { }
        }
        else if (!string.IsNullOrWhiteSpace(_filePath) && Directory.Exists(Path.GetDirectoryName(_filePath)))
        {
            try
            {
                Process.Start("explorer.exe", $"\"{Path.GetDirectoryName(_filePath)}\"");
            }
            catch { }
        }

        CloseToast();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CloseToast();
    }

    private void CloseToast()
    {
        _autoCloseTimer.Stop();
        try
        {
            Close();
        }
        catch { }
    }
}
