using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaskLocker.WPF.Services;
using TaskLocker.WPF.ViewModels;
using Serilog; // Нужно для LoggerConfiguration и RollingInterval
using Application = System.Windows.Application;

namespace TaskLocker.WPF
{
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            // Получаем путь к папке, где лежит exe-файл
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string logPath = Path.Combine(basePath, "logs", "log-.txt");

            // ИСПОЛЬЗУЕМ Serilog.Log ЯВНО, чтобы убрать конфликт
            Serilog.Log.Logger = new LoggerConfiguration()
                .WriteTo.File(logPath,
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: 30)
                .CreateLogger();

            _host = Host.CreateDefaultBuilder()
                .UseSerilog() // Подключаем Serilog
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IWindowManagementService, PInvokeWindowService>();
                    services.AddHostedService<ShutdownDialogMonitorService>();
                    services.AddSingleton<MainViewModel>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            await _host.StartAsync();
            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();

            // И здесь тоже явно указываем Serilog.Log
            Serilog.Log.CloseAndFlush();

            base.OnExit(e);
        }
    }
}