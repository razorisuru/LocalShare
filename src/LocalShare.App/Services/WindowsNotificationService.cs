using System.Diagnostics;
using System.IO;
using System.Windows;
using LocalShare.Core.Interfaces;
using LocalShare.App.Views;

namespace LocalShare.App.Services;

public class WindowsNotificationService : INotificationService
{
    public void ShowFileReceivedNotification(string senderName, string fileName, string filePath)
    {
        string title = $"📥 File Received from {senderName}";
        ShowNotification(title, fileName, filePath);
    }

    public void ShowNotification(string title, string message, string? filePath = null)
    {
        try
        {
            // Display WPF Floating Glass Desktop Notification Toast
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var toast = new NotificationToastWindow(title, message, filePath);
                    toast.Show();
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Notification toast error: {ex.Message}");
        }
    }
}
