using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BaoToolsGui.Views;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace BaoToolsGui.Services;

/// <summary>
/// Drop-in modern Fluent replacement for System.Windows.MessageBox.
/// Safe to call from any thread and ensures consistent dark Mica styling with WPF-UI.
/// </summary>
public static class ModernMessageBox
{
    /// <summary>
    /// Displays a modal Fluent message box dialog centered on the owner or main window.
    /// </summary>
    public static MessageBoxResult Show(
        string messageBoxText,
        string? caption = null,
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None,
        Window? owner = null,
        string? primaryText = null,
        string? secondaryText = null)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(() => Show(
                messageBoxText, caption, button, icon, defaultResult, owner, primaryText, secondaryText));
        }

        var dialog = new ModernDialogWindow(
            messageBoxText,
            caption,
            button,
            icon,
            defaultResult,
            primaryText,
            secondaryText);

        var targetOwner = owner
            ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
            ?? Application.Current?.MainWindow;

        if (targetOwner != null && targetOwner.IsVisible)
        {
            dialog.Owner = targetOwner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Topmost = true;
        }

        MainWindow? mainWindow = targetOwner as MainWindow ?? Application.Current?.MainWindow as MainWindow;
        bool usedScrim = false;
        double origOwnerOpacity = targetOwner?.Opacity ?? 1.0;

        try
        {
            if (mainWindow != null && mainWindow.IsVisible && mainWindow.DialogDimScrim != null)
            {
                mainWindow.DialogDimScrim.Visibility = Visibility.Visible;
                usedScrim = true;
            }
            else if (targetOwner != null && targetOwner != mainWindow)
            {
                targetOwner.Opacity = 0.65;
            }

            dialog.ShowDialog();
        }
        finally
        {
            if (usedScrim && mainWindow?.DialogDimScrim != null)
            {
                mainWindow.DialogDimScrim.Visibility = Visibility.Collapsed;
            }
            if (targetOwner != null && targetOwner != mainWindow)
            {
                targetOwner.Opacity = origOwnerOpacity;
            }
        }

        return dialog.Result;
    }

    /// <summary>
    /// Displays a modal Fluent message box dialog with an explicit owner window.
    /// </summary>
    public static MessageBoxResult Show(
        Window owner,
        string messageBoxText,
        string? caption = null,
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None) =>
        Show(messageBoxText, caption, button, icon, defaultResult, owner);

    /// <summary>
    /// Asynchronously displays a modal Fluent message box dialog.
    /// </summary>
    public static Task<MessageBoxResult> ShowAsync(
        string messageBoxText,
        string? caption = null,
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None,
        Window? owner = null,
        string? primaryText = null,
        string? secondaryText = null)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return Task.FromResult(MessageBoxResult.Cancel);

        return dispatcher.InvokeAsync(() => Show(
            messageBoxText, caption, button, icon, defaultResult, owner, primaryText, secondaryText)).Task;
    }
}
