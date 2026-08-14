namespace LocalShare.Core.Interfaces;

public interface INotificationService
{
    void ShowFileReceivedNotification(string senderName, string fileName, string filePath);
    void ShowNotification(string title, string message, string? filePath = null);
}
