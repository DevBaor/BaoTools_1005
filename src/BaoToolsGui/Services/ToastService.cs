using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace BaoToolsGui.Services;

/// <summary>
/// App-wide bottom-right toast feedback. Thin wrapper over Wpf.Ui's SnackbarService. The presenter
/// is attached once from MainWindow (see App.OnStartup). Safe to call from any thread.
/// </summary>
public class ToastService
{
    private readonly SnackbarService _snackbar = new();
    private SnackbarPresenter? _presenter;

    /// <summary>Wire the presenter that hosts the toasts (called once after the window is built).</summary>
    public void Attach(SnackbarPresenter presenter)
    {
        _presenter = presenter;
        _snackbar.SetSnackbarPresenter(presenter);
    }

    /// <summary>Show a transient toast (auto-dismiss). Marshals to the UI thread; no-ops if unattached.</summary>
    public void Show(string title, string message, bool error = false)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        void Post() => _snackbar.Show(
            title, message,
            error ? ControlAppearance.Caution : ControlAppearance.Secondary,
            null,
            TimeSpan.FromSeconds(3));

        if (dispatcher.CheckAccess()) Post();
        else dispatcher.Invoke(Post);
    }

    /// <summary>
    /// Show a PERSISTENT toast with an action button (no auto-dismiss; user-closable). Used for the
    /// "update ready" prompt: the action runs <paramref name="onAction"/> (e.g. restart).
    /// </summary>
    public void ShowAction(string title, string message, string actionLabel, Action onAction, bool error = false)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        void Post()
        {
            if (_presenter is null) return;
            var isUpdate = (title + message + actionLabel).Contains("update", StringComparison.OrdinalIgnoreCase)
                        || (title + message + actionLabel).Contains("cập nhật", StringComparison.OrdinalIgnoreCase);

            var icon = new SymbolIcon(isUpdate ? SymbolRegular.ArrowDownload24 : SymbolRegular.ArrowSync24)
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0xa5, 0xfa)),
                FontSize = 20
            };

            var bar = new Snackbar(_presenter)
            {
                Title = title,
                Appearance = error ? ControlAppearance.Caution : ControlAppearance.Secondary,
                Icon = icon,
                // No "infinite" sentinel exists: Timeout is how long it's VISIBLE (Zero = dismiss
                // instantly), so use a very large value to effectively persist until acted on / closed.
                Timeout = TimeSpan.FromDays(1),
                IsCloseButtonEnabled = true,
                MaxWidth = 440,
            };

            // Build the body: message + a real action button with proper padding and height
            var actionBtn = new Wpf.Ui.Controls.Button
            {
                Content = actionLabel,
                Height = 32,
                Padding = new Thickness(16, 4, 16, 4),
                Margin = new Thickness(0, 10, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(6),
            };
            // The action tears down the app or applies update; close button handles manual dismiss.
            actionBtn.Click += (_, _) => onAction();

            bar.Content = new StackPanel
            {
                Margin = new Thickness(0, 2, 0, 4),
                Children =
                {
                    new System.Windows.Controls.TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 19,
                        FontSize = 13,
                    },
                    actionBtn,
                },
            };
            bar.Show();
        }

        if (dispatcher.CheckAccess()) Post();
        else dispatcher.Invoke(Post);
    }
}
