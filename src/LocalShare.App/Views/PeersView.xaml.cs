using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LocalShare.App.ViewModels;
using LocalShare.Core.Models;

namespace LocalShare.App.Views;

public partial class PeersView : UserControl
{
    public PeersView()
    {
        InitializeComponent();
    }

    private void UserControl_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && DataContext is PeersViewModel vm)
            {
                vm.HandleDroppedFiles(files);
            }
        }
    }

    private void UserControl_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void PeerCard_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && sender is FrameworkElement element && element.DataContext is Peer targetPeer)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && DataContext is PeersViewModel vm)
            {
                vm.HandleDroppedFiles(files, targetPeer);
            }
        }
    }

    private void PeerCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Peer clickedPeer && DataContext is PeersViewModel vm)
        {
            vm.TogglePeerSelectionCommand.Execute(clickedPeer);
        }
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            // If focused element is a TextBox and clipboard contains text (not image), let TextBox handle normal text paste
            if (Keyboard.FocusedElement is TextBox && Clipboard.ContainsText() && !Clipboard.ContainsImage())
            {
                return;
            }

            if (DataContext is PeersViewModel vm)
            {
                vm.PasteFromClipboardCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
