using System.Windows.Controls;
using BaoToolsGui.ViewModels;

namespace BaoToolsGui.Views;

public partial class ModeView : UserControl
{
    public ModeView(ModeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
