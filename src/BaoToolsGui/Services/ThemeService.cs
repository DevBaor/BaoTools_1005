using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace BaoToolsGui.Services;

public record ThemeDefinition(
    string Id,
    string DisplayKey,
    Color AccentColor,
    Color BackgroundColor,
    Color CardColor,
    Color TextColor,
    Color SecondaryTextColor,
    WindowBackdropType BackdropType)
{
    public string Display => Resources.Strings.Get(DisplayKey);
}

public class ThemeService
{
    private readonly SettingsService _settings;
    private static ResourceDictionary? _themeResourceDict;

    public static readonly List<ThemeDefinition> AvailableThemes = new()
    {
        new ThemeDefinition(
            "Default",
            nameof(Resources.Strings.Theme_Default),
            Color.FromRgb(0x7c, 0x3a, 0xed), // #7c3aed
            Color.FromRgb(0x18, 0x18, 0x1b), // #18181b
            Color.FromRgb(0x27, 0x27, 0x2a), // #27272a
            Color.FromRgb(0xf4, 0xf4, 0xf5), // #f4f4f5
            Color.FromRgb(0xa1, 0xa1, 0xaa), // #a1a1aa
            WindowBackdropType.Mica),

        new ThemeDefinition(
            "Dracula",
            nameof(Resources.Strings.Theme_Dracula),
            Color.FromRgb(0xbd, 0x93, 0xf9), // #bd93f9 Dracula Purple
            Color.FromRgb(0x28, 0x2a, 0x36), // #282a36 Dracula Base
            Color.FromRgb(0x44, 0x47, 0x5a), // #44475a Current Line / Card
            Color.FromRgb(0xf8, 0xf8, 0xf2), // #f8f8f2 Foreground
            Color.FromRgb(0x62, 0x72, 0xa4), // #6272a4 Comment / Secondary
            WindowBackdropType.None),

        new ThemeDefinition(
            "TokyoNight",
            nameof(Resources.Strings.Theme_TokyoNight),
            Color.FromRgb(0x7a, 0xa2, 0xf7), // #7aa2f7 Tokyo Neon Sky
            Color.FromRgb(0x1a, 0x1b, 0x26), // #1a1b26 Night Base
            Color.FromRgb(0x24, 0x28, 0x3b), // #24283b Card Surface
            Color.FromRgb(0xc0, 0xca, 0xf5), // #c0caf5 Text
            Color.FromRgb(0x79, 0x82, 0xa9), // #7982a9 Secondary
            WindowBackdropType.None),

        new ThemeDefinition(
            "Synthwave",
            nameof(Resources.Strings.Theme_Synthwave),
            Color.FromRgb(0xfe, 0x44, 0x50), // #fe4450 Sunset Neon Red/Orange
            Color.FromRgb(0x26, 0x23, 0x35), // #262335 Synthwave Deep Space
            Color.FromRgb(0x34, 0x29, 0x4f), // #34294f Purple Card
            Color.FromRgb(0xf4, 0xee, 0xe4), // #f4eee4 Text
            Color.FromRgb(0xff, 0x7e, 0xdb), // #ff7edb Hot Pink
            WindowBackdropType.None),

        new ThemeDefinition(
            "RosePine",
            nameof(Resources.Strings.Theme_RosePine),
            Color.FromRgb(0xeb, 0x6f, 0x92), // #eb6f92 Love Rose
            Color.FromRgb(0x19, 0x17, 0x24), // #191724 SoHo Base
            Color.FromRgb(0x26, 0x23, 0x3a), // #26233a Card Surface
            Color.FromRgb(0xe0, 0xde, 0xf4), // #e0def4 Text
            Color.FromRgb(0x90, 0x8c, 0xaa), // #908caa Secondary
            WindowBackdropType.None),

        new ThemeDefinition(
            "Sakura",
            nameof(Resources.Strings.Theme_Sakura),
            Color.FromRgb(0xff, 0x75, 0x97), // #ff7597 Sakura Pink
            Color.FromRgb(0x1f, 0x16, 0x27), // #1f1627 Velvet Plum Base
            Color.FromRgb(0x2d, 0x20, 0x38), // #2d2038 Plum Card
            Color.FromRgb(0xfc, 0xe7, 0xf3), // #fce7f3 Text
            Color.FromRgb(0xc0, 0x84, 0xfc), // #c084fc Lavender
            WindowBackdropType.None),

        new ThemeDefinition(
            "Emerald",
            nameof(Resources.Strings.Theme_Emerald),
            Color.FromRgb(0x00, 0xf5, 0x9b), // #00f59b Matrix Emerald
            Color.FromRgb(0x0a, 0x13, 0x10), // #0a1310 Obsidian Jade Base
            Color.FromRgb(0x13, 0x24, 0x1e), // #13241e Forest Card
            Color.FromRgb(0xec, 0xfd, 0xf5), // #ecfdf5 Text
            Color.FromRgb(0x6e, 0xe7, 0xb7), // #6ee7b7 Mint Secondary
            WindowBackdropType.None),

        new ThemeDefinition(
            "AmberSunset",
            nameof(Resources.Strings.Theme_AmberSunset),
            Color.FromRgb(0xf5, 0x9e, 0x0b), // #f59e0b Amber Gold
            Color.FromRgb(0x18, 0x15, 0x12), // #181512 Dark Wood Base
            Color.FromRgb(0x26, 0x20, 0x1a), // #26201a Roasted Card
            Color.FromRgb(0xfe, 0xf3, 0xc7), // #fef3c7 Warm Text
            Color.FromRgb(0xd9, 0x77, 0x06), // #d97706 Copper Secondary
            WindowBackdropType.None),

        new ThemeDefinition(
            "Amethyst",
            nameof(Resources.Strings.Theme_Amethyst),
            Color.FromRgb(0xa8, 0x55, 0xf7), // #a855f7 Electric Amethyst
            Color.FromRgb(0x12, 0x0d, 0x1d), // #120d1d Deep Violet Base
            Color.FromRgb(0x1e, 0x15, 0x30), // #1e1530 Card
            Color.FromRgb(0xfa, 0xf5, 0xff), // #faf5ff Text
            Color.FromRgb(0xc0, 0x84, 0xfc), // #c084fc Lavender
            WindowBackdropType.None),

        new ThemeDefinition(
            "AbyssalOcean",
            nameof(Resources.Strings.Theme_AbyssalOcean),
            Color.FromRgb(0x38, 0xbd, 0xf8), // #38bdf8 Deep Cyan Wave
            Color.FromRgb(0x07, 0x15, 0x21), // #071521 Abyssal Ocean Base
            Color.FromRgb(0x0e, 0x22, 0x33), // #0e2233 Marine Card
            Color.FromRgb(0xf0, 0xf9, 0xff), // #f0f9ff Text
            Color.FromRgb(0x7d, 0xd3, 0xfc), // #7dd3fc Sky Secondary
            WindowBackdropType.None),

        new ThemeDefinition(
            "Cyberpunk",
            nameof(Resources.Strings.Theme_Cyberpunk),
            Color.FromRgb(0x00, 0xf0, 0xff), // #00f0ff Neon Cyan
            Color.FromRgb(0x0d, 0x0d, 0x15), // #0d0d15 Deep Dark
            Color.FromRgb(0x1a, 0x1a, 0x2e), // #1a1a2e Card
            Color.FromRgb(0xfe, 0xe7, 0x15), // #fee715 Neon Gold
            Color.FromRgb(0x94, 0xa3, 0xb8), // #94a3b8
            WindowBackdropType.None),

        new ThemeDefinition(
            "Nord",
            nameof(Resources.Strings.Theme_Nord),
            Color.FromRgb(0x88, 0xc0, 0xd0), // #88c0d0 Frost Cyan
            Color.FromRgb(0x2e, 0x34, 0x40), // #2e3440 Polar Night
            Color.FromRgb(0x3b, 0x42, 0x52), // #3b4252 Surface
            Color.FromRgb(0xec, 0xef, 0xf4), // #eceff4
            Color.FromRgb(0xd8, 0xde, 0xe9), // #d8dee9
            WindowBackdropType.None),

        new ThemeDefinition(
            "Catppuccin",
            nameof(Resources.Strings.Theme_Catppuccin),
            Color.FromRgb(0xcb, 0xa6, 0xf7), // #cba6f7 Mauve
            Color.FromRgb(0x1e, 0x1e, 0x2e), // #1e1e2e Base
            Color.FromRgb(0x31, 0x32, 0x44), // #313244 Surface0
            Color.FromRgb(0xcd, 0xd6, 0xf4), // #cdd6f4 Text
            Color.FromRgb(0xa6, 0xad, 0xc8), // #a6adc8 Subtext0
            WindowBackdropType.None),

        new ThemeDefinition(
            "OneDark",
            nameof(Resources.Strings.Theme_OneDark),
            Color.FromRgb(0x61, 0xaf, 0xef), // #61afef One Dark Cyan-Blue
            Color.FromRgb(0x21, 0x25, 0x2b), // #21252b Atom Deep Gray
            Color.FromRgb(0x28, 0x2c, 0x34), // #282c34 Atom Card
            Color.FromRgb(0xab, 0xb2, 0xbf), // #abb2bf Text
            Color.FromRgb(0x5c, 0x63, 0x70), // #5c6370 Secondary
            WindowBackdropType.None),

        new ThemeDefinition(
            "Gruvbox",
            nameof(Resources.Strings.Theme_Gruvbox),
            Color.FromRgb(0xfe, 0x80, 0x19), // #fe8019 Retro Bright Orange
            Color.FromRgb(0x28, 0x28, 0x28), // #282828 Warm Retro Dark
            Color.FromRgb(0x3c, 0x38, 0x36), // #3c3836 Gruvbox Surface
            Color.FromRgb(0xeb, 0xdb, 0xb2), // #ebdbb2 Text
            Color.FromRgb(0xa8, 0x99, 0x84), // #a89984 Secondary
            WindowBackdropType.None),

        new ThemeDefinition(
            "Midnight",
            nameof(Resources.Strings.Theme_Midnight),
            Color.FromRgb(0x3b, 0x82, 0xf6), // #3b82f6 Electric Blue
            Color.FromRgb(0x00, 0x00, 0x00), // #000000 True Black
            Color.FromRgb(0x12, 0x12, 0x12), // #121212 Card
            Color.FromRgb(0xff, 0xff, 0xff), // #ffffff Pure White
            Color.FromRgb(0x9c, 0xa3, 0xaf), // #9ca3af
            WindowBackdropType.None)
    };

    public ThemeService(SettingsService settings)
    {
        _settings = settings;
    }

    public string CurrentThemeId => _settings.Theme;

    public void ApplyCurrentTheme()
    {
        ApplyTheme(_settings.Theme);
    }

    public void ApplyTheme(string themeId)
    {
        var theme = AvailableThemes.Find(t => string.Equals(t.Id, themeId, StringComparison.OrdinalIgnoreCase))
                    ?? AvailableThemes[0];

        _settings.Theme = theme.Id;

        // Apply Wpf.Ui Accent color
        ApplicationAccentColorManager.Apply(theme.AccentColor, ApplicationTheme.Dark);

        // Update dynamic application resources
        var app = Application.Current;
        if (app is null) return;

        if (_themeResourceDict is null)
        {
            _themeResourceDict = new ResourceDictionary();
            app.Resources.MergedDictionaries.Add(_themeResourceDict);
        }

        var dict = _themeResourceDict;

        // Theme brushes
        var bgBrush = new SolidColorBrush(theme.BackgroundColor);
        var cardBrush = new SolidColorBrush(theme.CardColor);
        var textBrush = new SolidColorBrush(theme.TextColor);
        var secTextBrush = new SolidColorBrush(theme.SecondaryTextColor);
        var accentBrush = new SolidColorBrush(theme.AccentColor);

        bgBrush.Freeze();
        cardBrush.Freeze();
        textBrush.Freeze();
        secTextBrush.Freeze();
        accentBrush.Freeze();

        dict["AppBackgroundBrush"] = bgBrush;
        dict["AppCardBackgroundBrush"] = cardBrush;
        dict["AppForegroundBrush"] = textBrush;
        dict["AppSecondaryForegroundBrush"] = secTextBrush;
        dict["AppAccentBrush"] = accentBrush;

        // Override Wpf.Ui standard dark tokens if not default
        if (theme.Id != "Default")
        {
            var cardStrokeBrush = new SolidColorBrush(Color.FromArgb(0x35, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B));
            cardStrokeBrush.Freeze();

            byte paneR = (byte)Math.Max(0, theme.BackgroundColor.R - 8);
            byte paneG = (byte)Math.Max(0, theme.BackgroundColor.G - 8);
            byte paneB = (byte)Math.Max(0, theme.BackgroundColor.B - 8);
            var paneBrush = new SolidColorBrush(Color.FromRgb(paneR, paneG, paneB));
            paneBrush.Freeze();

            dict["ApplicationBackgroundBrush"] = bgBrush;
            dict["WindowBackgroundFillColorDefaultBrush"] = bgBrush;
            dict["SolidBackgroundFillColorBaseBrush"] = bgBrush;
            dict["CardBackgroundFillColorDefaultBrush"] = cardBrush;
            dict["CardBackgroundFillColorSecondaryBrush"] = cardBrush;
            dict["CardStrokeColorDefaultBrush"] = cardStrokeBrush;
            dict["NavigationViewDefaultPaneBackground"] = paneBrush;
            dict["NavigationViewContentBackground"] = bgBrush;
            dict["ControlElevationBorderBrush"] = cardStrokeBrush;
            dict["TextFillColorPrimaryBrush"] = textBrush;
            dict["TextFillColorSecondaryBrush"] = secTextBrush;
            dict["SystemAccentColorPrimaryBrush"] = accentBrush;
        }
        else
        {
            dict.Remove("ApplicationBackgroundBrush");
            dict.Remove("WindowBackgroundFillColorDefaultBrush");
            dict.Remove("SolidBackgroundFillColorBaseBrush");
            dict.Remove("CardBackgroundFillColorDefaultBrush");
            dict.Remove("CardBackgroundFillColorSecondaryBrush");
            dict.Remove("CardStrokeColorDefaultBrush");
            dict.Remove("NavigationViewDefaultPaneBackground");
            dict.Remove("NavigationViewContentBackground");
            dict.Remove("ControlElevationBorderBrush");
            dict.Remove("TextFillColorPrimaryBrush");
            dict.Remove("TextFillColorSecondaryBrush");
            dict.Remove("SystemAccentColorPrimaryBrush");
        }

        // Apply backdrop to main window if open
        if (app.MainWindow is FluentWindow win)
        {
            win.WindowBackdropType = theme.BackdropType;
            if (theme.Id != "Default")
            {
                win.Background = bgBrush;
            }
            else
            {
                win.ClearValue(Window.BackgroundProperty);
            }
        }
    }
}
