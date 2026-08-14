using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Appearance;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;
using LocalShare.Data;
using LocalShare.Data.Repositories;
using LocalShare.Networking.Discovery;
using LocalShare.Networking.Http;
using LocalShare.Networking.PublicSpace;
using LocalShare.Networking.Transfer;
using LocalShare.Networking.Chat;
using LocalShare.Networking.Services;
using LocalShare.App.ViewModels;
using LocalShare.App.Views;
using LocalShare.App.Helpers;
using LocalShare.App.Services;

namespace LocalShare.App;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Apply WPF-UI Dark Theme
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);

            // 1. Initialize SQLite Database
            var dbInit = new DatabaseInitializer();
            await dbInit.InitializeAsync();

            // 2. Load Local User Profile
            var sqliteRepo = new SqliteRepositories(dbInit);
            var profile = await sqliteRepo.GetProfileAsync();

            // 3. Configure DI Container
            var services = new ServiceCollection();
            services.AddSingleton(dbInit);
            services.AddSingleton(sqliteRepo);
            services.AddSingleton<IProfileRepository>(sqliteRepo);
            services.AddSingleton<IPeerRepository>(sqliteRepo);
            services.AddSingleton<IMessageRepository>(sqliteRepo);
            services.AddSingleton<IGroupRepository>(sqliteRepo);
            services.AddSingleton<ITransferRepository>(sqliteRepo);

            services.AddSingleton(profile);

            // Notification Service
            services.AddSingleton<INotificationService, WindowsNotificationService>();

            // Networking Services
            services.AddSingleton<PeerRegistry>();
            services.AddSingleton<IDiscoveryService, UdpBeaconService>();
            services.AddSingleton<ITransferService, TransferService>();
            services.AddSingleton<IPublicSpaceService, PublicSpaceService>();
            services.AddSingleton<IChatService, ChatService>();
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<KestrelServerHost>();

            // ViewModels
            services.AddSingleton<ShellViewModel>();
            services.AddSingleton<PeersViewModel>();
            services.AddSingleton<ChatViewModel>();
            services.AddSingleton<PublicSpaceViewModel>();
            services.AddSingleton<GroupsViewModel>();
            services.AddSingleton<TransfersViewModel>();
            services.AddSingleton<ProfileSettingsViewModel>();

            // Views
            services.AddSingleton<ShellView>();

            ServiceProvider = services.BuildServiceProvider();

            // 4. Subscribe to FileReceived and MessageReceived events for Windows Notifications
            var transferService = ServiceProvider.GetRequiredService<ITransferService>();
            var notificationService = ServiceProvider.GetRequiredService<INotificationService>();
            var chatService = ServiceProvider.GetRequiredService<IChatService>();

            transferService.FileReceived += (sender, transfer) =>
            {
                if (profile.EnableNotifications)
                {
                    notificationService.ShowFileReceivedNotification(
                        transfer.PeerDisplayName,
                        transfer.FileName,
                        transfer.FilePath
                    );
                }
            };

            chatService.MessageReceived += (sender, msg) =>
            {
                if (profile.EnableNotifications && msg.SenderDeviceId != profile.DeviceId)
                {
                    var title = $"💬 {msg.SenderDisplayName}";
                    var body = string.IsNullOrWhiteSpace(msg.Body) && !string.IsNullOrWhiteSpace(msg.AttachmentFileName)
                        ? $"📎 Sent a file: {msg.AttachmentFileName}"
                        : msg.Body;
                    notificationService.ShowNotification(title, body);
                }
            };

            // 5. Start Kestrel Server Host (HTTP & SignalR)
            var kestrelHost = ServiceProvider.GetRequiredService<KestrelServerHost>();
            await kestrelHost.StartAsync();

            // 6. Start UDP Discovery Beacon
            var discoveryService = ServiceProvider.GetRequiredService<IDiscoveryService>();
            await discoveryService.StartAsync();

            // 7. Show Shell Window
            var shellView = ServiceProvider.GetRequiredService<ShellView>();
            shellView.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup Error: {ex.Message}\n{ex.StackTrace}", "LocalShare Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (ServiceProvider != null)
        {
            var discovery = ServiceProvider.GetService<IDiscoveryService>();
            if (discovery != null) await discovery.StopAsync();

            var kestrel = ServiceProvider.GetService<KestrelServerHost>();
            if (kestrel != null) await kestrel.StopAsync();

            var notificationService = ServiceProvider.GetService<INotificationService>();
            if (notificationService is IDisposable disposableNotification)
            {
                disposableNotification.Dispose();
            }
        }

        base.OnExit(e);
    }
}
