using System.Windows.Controls;
using BaoToolsGui.ViewModels;

namespace BaoToolsGui.Views;

public partial class TicketsView : UserControl
{
    public TicketsView(TicketsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) =>
        {
            viewModel.CheckSteamStatus();
            await viewModel.LoadDenuvoGamesAsync();
        };
    }
}
