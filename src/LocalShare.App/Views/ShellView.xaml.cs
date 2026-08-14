using System.Windows;
using LocalShare.App.ViewModels;

namespace LocalShare.App.Views;

public partial class ShellView : Window
{
    public ShellView(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
