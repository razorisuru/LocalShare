using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalShare.Core.Interfaces;
using LocalShare.Core.Models;

namespace LocalShare.App.ViewModels;

public partial class TransfersViewModel : ObservableObject
{
    private readonly ITransferService _transferService;

    [ObservableProperty]
    private ObservableCollection<TransferItem> _transfers = new();

    public TransfersViewModel(ITransferService transferService)
    {
        _transferService = transferService;
        _transferService.TransferProgressChanged += OnTransferProgressChanged;

        _ = LoadTransfersAsync();
    }

    private async Task LoadTransfersAsync()
    {
        var logs = await _transferService.GetTransferLogsAsync();
        Transfers.Clear();
        foreach (var t in logs) Transfers.Add(t);
    }

    private void OnTransferProgressChanged(object? sender, TransferItem item)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            var existing = Transfers.FirstOrDefault(t => t.Id == item.Id);
            if (existing != null)
            {
                int index = Transfers.IndexOf(existing);
                Transfers[index] = item;
            }
            else
            {
                Transfers.Insert(0, item);
            }
        });
    }

    [RelayCommand]
    private async Task PauseTransferAsync(TransferItem? item)
    {
        if (item == null) return;
        await _transferService.PauseTransferAsync(item.Id);
    }

    [RelayCommand]
    private async Task ResumeTransferAsync(TransferItem? item)
    {
        if (item == null) return;
        await _transferService.ResumeTransferAsync(item.Id);
    }

    [RelayCommand]
    private async Task ClearAllTransfersAsync()
    {
        await _transferService.ClearAllTransferLogsAsync();
        Transfers.Clear();
    }
}
