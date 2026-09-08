using System.Windows.Controls;
using BaoToolsGui.ViewModels;

namespace BaoToolsGui.Views;

public partial class OnlineFixesView : UserControl
{
    private readonly OnlineFixesViewModel _viewModel;

    public OnlineFixesView(OnlineFixesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }
}
