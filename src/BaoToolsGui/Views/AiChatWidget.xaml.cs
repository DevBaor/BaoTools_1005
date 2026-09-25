using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BaoToolsGui.Services;
using BaoToolsGui.ViewModels;

namespace BaoToolsGui.Views;

public partial class AiChatWidget : UserControl
{
    public static readonly DependencyProperty IsAssistantHoveredProperty =
        DependencyProperty.Register(nameof(IsAssistantHovered), typeof(bool), typeof(AiChatWidget), new PropertyMetadata(false));

    public bool IsAssistantHovered
    {
        get => (bool)GetValue(IsAssistantHoveredProperty);
        set => SetValue(IsAssistantHoveredProperty, value);
    }

    private readonly System.Windows.Threading.DispatcherTimer _rotationTimer;
    private readonly System.Windows.Threading.DispatcherTimer _resumeDelayTimer;
    private int _currentMessageIndex = 0;
    private bool _isHoverPaused = false;

    public AiChatWidget()
    {
        InitializeComponent();

        CommandBindings.Add(new CommandBinding(Markdig.Wpf.Commands.Hyperlink, OpenHyperlink));
        AddHandler(Hyperlink.RequestNavigateEvent, new System.Windows.Navigation.RequestNavigateEventHandler((_, e) => e.Handled = true));

        // ── Rotating Discovery Timer (5 seconds interval) ──────────────────
        _rotationTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5.0)
        };
        _rotationTimer.Tick += (_, _) => RotateToNextMessage();

        // ── Resume Rotation Delay Timer (800ms after pointer leaves) ────────
        _resumeDelayTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        _resumeDelayTimer.Tick += (_, _) => OnResumeDelayTick();

        ChatWindow.IsVisibleChanged += (s, e) =>
        {
            if (ChatWindow.Visibility == Visibility.Visible)
            {
                // Assistant is open: stop rotation timer immediately
                _rotationTimer.Stop();
                _resumeDelayTimer.Stop();
                ResetBotNudgeAnimation();

                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var slide = new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                ChatWindow.BeginAnimation(UIElement.OpacityProperty, fade);
                ChatTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slide);
            }
            else
            {
                // Assistant closed: resume rotation if not hovered
                if (!_isHoverPaused && DataContext is AiChatViewModel vm && !vm.IsOpen)
                {
                    _rotationTimer.Stop();
                    _rotationTimer.Start();
                }
            }
        };

        // ── Hover Interactions for Bubble & Launcher Button ─────────────────
        DiscoveryBubbleButton.MouseEnter += (_, _) => OnAssistantPointerEnter();
        DiscoveryBubbleButton.MouseLeave += (_, _) => OnAssistantPointerLeave();

        AiLauncherButton.MouseEnter += (_, _) => OnAssistantPointerEnter();
        AiLauncherButton.MouseLeave += (_, _) => OnAssistantPointerLeave();

        Loaded += (_, _) =>
        {
            if (DataContext is AiChatViewModel vm)
            {
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(AiChatViewModel.IsOpen))
                    {
                        if (vm.IsOpen)
                        {
                            _rotationTimer.Stop();
                            _resumeDelayTimer.Stop();
                            ResetBotNudgeAnimation();
                        }
                        else if (!_isHoverPaused)
                        {
                            _rotationTimer.Stop();
                            _rotationTimer.Start();
                        }
                    }
                };

                // Start rotation when loaded and chat is closed
                if (!vm.IsOpen && !_isHoverPaused)
                {
                    _rotationTimer.Start();
                }

                vm.RequestScrollToBottom += () =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        ChatScroller.ScrollToEnd();
                    });
                };

                vm.RequestScrollToTop += () =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        ChatScroller.ScrollToHome();
                    });
                };
            }
        };

        Unloaded += (_, _) =>
        {
            _rotationTimer.Stop();
            _resumeDelayTimer.Stop();
        };
    }

    private void OnAssistantPointerEnter()
    {
        IsAssistantHovered = true;
        _isHoverPaused = true;
        _resumeDelayTimer.Stop();
        _rotationTimer.Stop();

        if (DataContext is AiChatViewModel vm)
        {
            vm.IsLauncherHovered = true;
        }
    }

    private void OnAssistantPointerLeave()
    {
        Dispatcher.InvokeAsync(() =>
        {
            // If pointer is still over either the button or the bubble, stay hovered
            if (DiscoveryBubbleButton.IsMouseOver || AiLauncherButton.IsMouseOver)
            {
                return;
            }

            IsAssistantHovered = false;
            if (DataContext is AiChatViewModel vm)
            {
                vm.IsLauncherHovered = false;
            }

            _resumeDelayTimer.Stop();
            _resumeDelayTimer.Start();
        });
    }

    private void OnResumeDelayTick()
    {
        _resumeDelayTimer.Stop();

        if (DiscoveryBubbleButton.IsMouseOver || AiLauncherButton.IsMouseOver)
        {
            return;
        }

        _isHoverPaused = false;
        if (DataContext is AiChatViewModel vm && !vm.IsOpen)
        {
            _rotationTimer.Start();
        }
    }

    private void RotateToNextMessage()
    {
        if (DataContext is not AiChatViewModel vm || vm.IsOpen)
        {
            _rotationTimer.Stop();
            return;
        }

        var messages = vm.GetDiscoveryMessages();
        if (messages == null || messages.Length == 0) return;

        _currentMessageIndex = (_currentMessageIndex + 1) % messages.Length;
        string nextText = messages[_currentMessageIndex];

        // Animate text transition: fade out + slide -3px, swap text, fade in + slide from +3px to 0
        if (DiscoveryBubbleButton.Template?.FindName("DiscoveryText", DiscoveryBubbleButton) is TextBlock textBlock &&
            DiscoveryBubbleButton.Template?.FindName("TextTranslate", DiscoveryBubbleButton) is TranslateTransform translate)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var slideOut = new DoubleAnimation(0, -3, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (_, _) =>
            {
                vm.CurrentDiscoveryMessage = nextText;
                translate.X = 3;

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var slideIn = new DoubleAnimation(3, 0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                textBlock.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                translate.BeginAnimation(TranslateTransform.XProperty, slideIn);

                TriggerBotNudgeAnimation();
            };

            textBlock.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            translate.BeginAnimation(TranslateTransform.XProperty, slideOut);
        }
        else
        {
            vm.CurrentDiscoveryMessage = nextText;
            TriggerBotNudgeAnimation();
        }
    }

    private void TriggerBotNudgeAnimation()
    {
        if (DataContext is AiChatViewModel vm && vm.IsOpen) return;
        if (LauncherNudgeTranslate == null || LauncherNudgeRotate == null) return;

        // Subtle vertical bounce: lifts up 3.5px, settles back
        var bounceAnimation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(450)
        };
        bounceAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-3.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
        bounceAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        });
        bounceAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-0.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        });
        bounceAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(450)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });

        // Playful subtle tilt/wobble: -3.5° -> +3.0° -> -1.5° -> 0°
        var rotateAnimation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(450)
        };
        rotateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-3.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100)))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
        rotateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(3.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
        rotateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-1.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(330)))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
        rotateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(450)))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });

        LauncherNudgeTranslate.BeginAnimation(TranslateTransform.YProperty, bounceAnimation, HandoffBehavior.SnapshotAndReplace);
        LauncherNudgeRotate.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation, HandoffBehavior.SnapshotAndReplace);

        // High-Tech Theme Glow Aura Pulse (Blooms outward and shines accent color)
        if (AiLauncherButton.Template?.FindName("LauncherGlowAura", AiLauncherButton) is Border glowAura &&
            AiLauncherButton.Template?.FindName("AuraScale", AiLauncherButton) is ScaleTransform auraScale)
        {
            var glowOpacityAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(700),
                FillBehavior = FillBehavior.Stop
            };
            glowOpacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            glowOpacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            glowOpacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });

            var auraScaleAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(700),
                FillBehavior = FillBehavior.Stop
            };
            auraScaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            auraScaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.35, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            auraScaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });

            glowOpacityAnim.Completed += (_, _) =>
            {
                glowAura.BeginAnimation(UIElement.OpacityProperty, null);
                auraScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                auraScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            };

            glowAura.BeginAnimation(UIElement.OpacityProperty, glowOpacityAnim, HandoffBehavior.SnapshotAndReplace);
            auraScale.BeginAnimation(ScaleTransform.ScaleXProperty, auraScaleAnim, HandoffBehavior.SnapshotAndReplace);
            auraScale.BeginAnimation(ScaleTransform.ScaleYProperty, auraScaleAnim, HandoffBehavior.SnapshotAndReplace);
        }
    }

    private void ResetBotNudgeAnimation()
    {
        if (LauncherNudgeTranslate != null)
        {
            LauncherNudgeTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            LauncherNudgeTranslate.Y = 0;
        }
        if (LauncherNudgeRotate != null)
        {
            LauncherNudgeRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            LauncherNudgeRotate.Angle = 0;
        }
        if (AiLauncherButton.Template?.FindName("LauncherGlowAura", AiLauncherButton) is Border glowAura &&
            AiLauncherButton.Template?.FindName("AuraScale", AiLauncherButton) is ScaleTransform auraScale)
        {
            glowAura.BeginAnimation(UIElement.OpacityProperty, null);
            glowAura.Opacity = 0;
            auraScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            auraScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            auraScale.ScaleX = 1;
            auraScale.ScaleY = 1;
        }
    }

    private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            if (DataContext is AiChatViewModel vm && vm.SendMessageCommand.CanExecute(null))
            {
                vm.SendMessageCommand.Execute(null);
            }
        }
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            if (DataContext is AiChatViewModel vm && vm.SendMessageCommand.CanExecute(null))
            {
                vm.SendMessageCommand.Execute(null);
            }
        }
    }

    private static void OpenHyperlink(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string url && !string.IsNullOrWhiteSpace(url))
        {
            try { SteamService.OpenUrl(url); } catch { }
        }
        e.Handled = true;
    }

    private void Markdown_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not UIElement el) return;
        e.Handled = true;
        el.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source = sender,
        });
    }
}
