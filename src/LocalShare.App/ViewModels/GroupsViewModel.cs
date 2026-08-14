using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;

namespace LocalShare.App.ViewModels;

public partial class GroupsViewModel : ObservableObject
{
    private readonly IGroupRepository _groupRepo;
    private readonly IDiscoveryService _discoveryService;
    private readonly IChatService _chatService;
    private readonly Profile _localProfile;

    [ObservableProperty]
    private ObservableCollection<Group> _groups = new();

    [ObservableProperty]
    private Group? _selectedGroup;

    [ObservableProperty]
    private string _newGroupName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Peer> _availablePeers = new();

    public GroupsViewModel(IGroupRepository groupRepo, IDiscoveryService discoveryService, IChatService chatService, Profile localProfile)
    {
        _groupRepo = groupRepo;
        _discoveryService = discoveryService;
        _chatService = chatService;
        _localProfile = localProfile;

        _ = LoadGroupsAsync();
    }

    private async Task LoadGroupsAsync()
    {
        var list = await _groupRepo.GetAllGroupsAsync();
        Groups.Clear();
        foreach (var g in list) Groups.Add(g);

        AvailablePeers.Clear();
        foreach (var p in _discoveryService.GetDiscoveredPeers()) AvailablePeers.Add(p);
    }

    [RelayCommand]
    private async Task CreateGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName)) return;

        var group = new Group
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = NewGroupName,
            CreatedByDeviceId = _localProfile.DeviceId,
            CreatedAt = DateTime.UtcNow
        };

        group.Members.Add(new GroupMember
        {
            GroupId = group.Id,
            DeviceId = _localProfile.DeviceId,
            DisplayName = _localProfile.DisplayName,
            JoinedAt = DateTime.UtcNow
        });

        await _groupRepo.SaveGroupAsync(group);
        Groups.Add(group);
        NewGroupName = string.Empty;
    }

    [RelayCommand]
    private async Task AddMemberToGroupAsync(Peer? peer)
    {
        if (SelectedGroup == null || peer == null) return;

        var member = new GroupMember
        {
            GroupId = SelectedGroup.Id,
            DeviceId = peer.DeviceId,
            DisplayName = peer.DisplayName,
            JoinedAt = DateTime.UtcNow
        };

        await _groupRepo.AddMemberAsync(SelectedGroup.Id, member);
        SelectedGroup.Members.Add(member);
    }
}
