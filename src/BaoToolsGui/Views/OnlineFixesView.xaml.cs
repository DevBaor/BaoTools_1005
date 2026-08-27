using BaoToolsGui.ViewModels;
using System.Windows.Controls;
using System.Windows;

namespace BaoToolsGui.Views;

public partial class OnlineFixesView : Page
{
    public OnlineFixesView(OnlineFixesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (s, e) => await viewModel.InitializeAsync();
    }
}
