using System.Windows.Controls;
using BaoToolsGui.ViewModels;

namespace BaoToolsGui.Views;

public partial class DownloadsView : UserControl
{
    public DownloadsView(DownloadsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
