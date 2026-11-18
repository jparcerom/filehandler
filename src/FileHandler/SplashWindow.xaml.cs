using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace FileHandler;

public partial class SplashWindow : Window
{
    public const double DisplayDurationSeconds = 2.5;

    public SplashWindow()
    {
        InitializeComponent();

        var version = typeof(SplashWindow).Assembly.GetName().Version
            ?? throw new InvalidOperationException("The application version is not available.");
        VersionTextBlock.Text = $"Versión {version.ToString(3)}";
    }

    public void StartLoadingAnimation()
    {
        var animation = new DoubleAnimation(
            100,
            TimeSpan.FromSeconds(DisplayDurationSeconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        StartupProgressBar.BeginAnimation(ProgressBar.ValueProperty, animation);
    }
}
