using System;
using System.Windows;
using System.Windows.Threading;

namespace SpotifyTaskbarWidget;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, _) => { };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            ex.SetObserved();
        };
    }
}
