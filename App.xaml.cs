using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaskLocker.WPF.Services;
using TaskLocker.WPF.ViewModels;

namespace TaskLocker.WPF
{
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IWindowManagementService, PInvokeWindowService>();
                    services.AddHostedService<ShutdownDialogMonitorService>();

                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton(sp => new MainWindow
                    {
                        DataContext = sp.GetRequiredService<MainViewModel>()
                    });
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // keep the app alive when all windows are closed
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            await _host.StartAsync();
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show(); // show the DI-created window
            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();
            base.OnExit(e);
        }
    }
}