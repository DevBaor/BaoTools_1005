using System;
using System.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace BaoToolsGui.Views;

/// <summary>
/// Frameless modern Fluent dialog modal matching BaoTools dark theme.
/// Replaces standard OS window chrome with a sleek, centered glowing card.
/// </summary>
public partial class ModernDialogWindow : Window
{
    private readonly MessageBoxButton _button;
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public ModernDialogWindow(
        string message,
        string? caption = null,
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None,
        string? primaryText = null,
        string? secondaryText = null)
    {
        _button = button;
        InitializeComponent();

        Title = caption ?? "BaoTools";
        TitleBlock.Text = string.IsNullOrWhiteSpace(caption) ? "BaoTools" : caption;
        MessageBlock.Text = message ?? "";

        // Setup Icon and Visual Tone
        ConfigureIcon(icon, caption, message);

        // Setup Buttons and Actions
        ConfigureButtons(button, defaultResult, icon, caption, message, primaryText, secondaryText);

        // Keyboard navigation (Esc to cancel)
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                CloseWithDefaultCancel();
                e.Handled = true;
            }
        };

        // Fallback result on close
        Closing += (_, _) =>
        {
            if (Result == MessageBoxResult.None)
            {
                Result = _button switch
                {
                    MessageBoxButton.OK => MessageBoxResult.OK,
                    MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
                    MessageBoxButton.YesNo => MessageBoxResult.No,
                    MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
                    _ => MessageBoxResult.Cancel
                };
            }
        };
    }

    private void ConfigureIcon(MessageBoxImage icon, string? caption, string? message)
    {
        var text = (caption ?? "") + " " + (message ?? "");
        bool isRestart = text.Contains("Restart", StringComparison.OrdinalIgnoreCase);
        bool isDelete = text.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
                        text.Contains("Remove", StringComparison.OrdinalIgnoreCase) ||
                        text.Contains("Clear", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (isRestart)
            {
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x24, 0x38));
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x4B, 0x75));
                SystemSounds.Question.Play();
                return;
            }

            if (isDelete)
            {
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x32, 0x16, 0x1A));
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x24, 0x2B));
                SystemSounds.Exclamation.Play();
                return;
            }

            switch (icon)
            {
                case MessageBoxImage.Warning:
                    IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x20, 0x12));
                    IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x3E, 0x18));
                    SystemSounds.Exclamation.Play();
                    break;

                case MessageBoxImage.Question:
                    IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x24, 0x38));
                    IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x4B, 0x75));
                    SystemSounds.Question.Play();
                    break;

                case MessageBoxImage.Error:
                    IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x32, 0x16, 0x1A));
                    IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x24, 0x2B));
                    SystemSounds.Hand.Play();
                    break;

                case MessageBoxImage.Information:
                    IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x24, 0x38));
                    IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x45, 0x6E));
                    SystemSounds.Asterisk.Play();
                    break;

                default:
                    IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2B));
                    IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x36, 0x36, 0x4D));
                    break;
            }
        }
        catch
        {
            // Ignore sound play failures
        }
    }

    private void ConfigureButtons(
        MessageBoxButton button,
        MessageBoxResult defaultResult,
        MessageBoxImage icon,
        string? caption,
        string? message,
        string? primaryText,
        string? secondaryText)
    {
        var text = (caption ?? "") + " " + (message ?? "");
        bool isRestart = text.Contains("Restart", StringComparison.OrdinalIgnoreCase);
        bool isDelete = text.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
                        text.Contains("Remove", StringComparison.OrdinalIgnoreCase) ||
                        text.Contains("Clear", StringComparison.OrdinalIgnoreCase);

        // Smart primary button text & color
        if (primaryText != null)
        {
            BtnAction.Content = primaryText;
        }
        else if (isRestart)
        {
            BtnAction.Content = "Restart";
        }
        else if (isDelete)
        {
            BtnAction.Content = caption?.Contains("lua", StringComparison.OrdinalIgnoreCase) == true ? "Remove" : "Delete";
        }
        else if (button == MessageBoxButton.YesNo || button == MessageBoxButton.YesNoCancel)
        {
            BtnAction.Content = "Yes";
        }
        else
        {
            BtnAction.Content = "OK";
        }

        if (isDelete)
        {
            BtnAction.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)); // Crimson red
        }
        else
        {
            BtnAction.Background = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)); // Blue accent
        }

        // Secondary button text
        string cancelText = secondaryText
            ?? (!string.IsNullOrWhiteSpace(BaoToolsGui.Resources.Strings.Builds_Action_Cancel)
                ? BaoToolsGui.Resources.Strings.Builds_Action_Cancel
                : "Cancel");

        BtnCancel.Content = cancelText;
        BtnNo.Content = secondaryText ?? "No";

        System.Windows.Controls.Button? defaultFocusButton = null;

        switch (button)
        {
            case MessageBoxButton.OK:
                BtnAction.Visibility = Visibility.Visible;
                defaultFocusButton = BtnAction;
                break;

            case MessageBoxButton.OKCancel:
                BtnCancel.Visibility = Visibility.Visible;
                BtnAction.Visibility = Visibility.Visible;
                defaultFocusButton = defaultResult == MessageBoxResult.Cancel ? BtnCancel : BtnAction;
                break;

            case MessageBoxButton.YesNo:
                BtnNo.Visibility = Visibility.Visible;
                BtnAction.Visibility = Visibility.Visible;
                defaultFocusButton = defaultResult == MessageBoxResult.No ? BtnNo : BtnAction;
                break;

            case MessageBoxButton.YesNoCancel:
                BtnCancel.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
                BtnAction.Visibility = Visibility.Visible;
                defaultFocusButton = defaultResult == MessageBoxResult.Cancel ? BtnCancel
                    : defaultResult == MessageBoxResult.No ? BtnNo : BtnAction;
                break;
        }

        Loaded += (_, _) => defaultFocusButton?.Focus();
    }

    private void CardBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void BtnCloseX_Click(object sender, RoutedEventArgs e) => CloseWithDefaultCancel();

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Cancel;
        Close();
    }

    private void BtnNo_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.No;
        Close();
    }

    private void BtnAction_Click(object sender, RoutedEventArgs e)
    {
        Result = (_button == MessageBoxButton.YesNo || _button == MessageBoxButton.YesNoCancel)
            ? MessageBoxResult.Yes
            : MessageBoxResult.OK;
        Close();
    }

    private void CloseWithDefaultCancel()
    {
        Result = _button switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            _ => MessageBoxResult.Cancel
        };
        Close();
    }
}
