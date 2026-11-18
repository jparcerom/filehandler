using System;
using System.Windows;

namespace FileHandler;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();

        var version = typeof(SplashWindow).Assembly.GetName().Version
            ?? throw new InvalidOperationException("The application version is not available.");
        VersionTextBlock.Text = $"Versión {version.ToString(3)}";
    }
}
