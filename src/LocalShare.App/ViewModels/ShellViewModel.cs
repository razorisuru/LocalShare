using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalShare.Common;
using LocalShare.Core.Models;

namespace LocalShare.App.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly Profile _localProfile;

    [ObservableProperty]
    private object _currentViewModel;

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _accentColor;

    [ObservableProperty]
    private string _appVersion = AppVersionInfo.DisplayVersion;

    public PeersViewModel PeersVM { get; }
    public ChatViewModel ChatVM { get; }
    public PublicSpaceViewModel PublicSpaceVM { get; }
    public GroupsViewModel GroupsVM { get; }
    public TransfersViewModel TransfersVM { get; }
    public ProfileSettingsViewModel ProfileSettingsVM { get; }

    public ShellViewModel(
        Profile localProfile,
        PeersViewModel peersVM,
        ChatViewModel chatVM,
        PublicSpaceViewModel publicSpaceVM,
        GroupsViewModel groupsVM,
        TransfersViewModel transfersVM,
        ProfileSettingsViewModel profileSettingsVM)
    {
        _localProfile = localProfile;

        PeersVM = peersVM;
        ChatVM = chatVM;
        PublicSpaceVM = publicSpaceVM;
        GroupsVM = groupsVM;
        TransfersVM = transfersVM;
        ProfileSettingsVM = profileSettingsVM;

        _currentViewModel = PeersVM;
        _displayName = _localProfile.DisplayName;
        _accentColor = _localProfile.AccentColor;

        PeersVM.RequestStartChat = async (targetPeer) =>
        {
            await ChatVM.OpenConversationWithPeerAsync(targetPeer);
            CurrentViewModel = ChatVM;
        };

        PeersVM.RequestOpenPublicSpace = (targetPeer) =>
        {
            PublicSpaceVM.SelectPeerAndLoadPublicSpace(targetPeer);
            CurrentViewModel = PublicSpaceVM;
        };
    }

    [RelayCommand]
    private void Navigate(string target)
    {
        CurrentViewModel = target switch
        {
            "Peers" => PeersVM,
            "Chat" => ChatVM,
            "PublicSpace" => PublicSpaceVM,
            "Groups" => GroupsVM,
            "Transfers" => TransfersVM,
            "Profile" => ProfileSettingsVM,
            _ => PeersVM
        };
    }
}
