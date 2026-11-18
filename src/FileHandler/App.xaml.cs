using System;
using System.Windows;
using System.Windows.Threading;

namespace FileHandler;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var splash = new SplashWindow();
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        splash.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            mainWindow.Show();
            splash.Close();
        };
        timer.Start();
    }
}
