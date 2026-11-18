using System;
using System.Windows;
using System.Windows.Threading;
using FileHandler.Localization;

namespace FileHandler;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        AppLanguage.Initialize();

        var splash = new SplashWindow();
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        splash.Show();

        splash.StartLoadingAnimation();

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(SplashWindow.DisplayDurationSeconds)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            mainWindow.Show();
            splash.Close();
        };
        timer.Start();
    }
}
