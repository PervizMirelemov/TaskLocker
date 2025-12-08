using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32; // Для работы с Реестром (Автозагрузка)
using TaskLocker.WPF.Services;
using TaskLocker.WPF.ViewModels;

// Явно указываем, что Application - это WPF Application
using Application = System.Windows.Application;

namespace TaskLocker.WPF
{
    public partial class App : Application
    {
        private const string AppName = "TaskLocker";
        private readonly IHost _host;
        private Mutex? _instanceMutex;

        public App()
        {
            // Настройка Хоста (как и раньше, но без ServiceBase)
            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Здесь можно добавить настройки, если нужно
                })
                .ConfigureServices((context, services) =>
                {
                    // Ваши сервисы
                    services.AddSingleton<IWindowManagementService, PInvokeWindowService>();
                    services.AddHostedService<ShutdownDialogMonitorService>();

                    // ViewModel регистрируем как Transient, чтобы обновлять состояние при каждом показе
                    services.AddTransient<MainViewModel>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // 1. ЗАЩИТА ОТ ПОВТОРНОГО ЗАПУСКА
            // Если приложение уже работает, второе просто закроется.
            const string mutexName = "Global\\TaskLocker_Unique_Mutex_ID";
            _instanceMutex = new Mutex(true, mutexName, out bool createdNew);
            if (!createdNew)
            {
                Shutdown();
                return;
            }

            // 2. САМОРЕГИСТРАЦИЯ В АВТОЗАГРУЗКУ (Работает как служба)
            // При каждом запуске проверяем, есть ли мы в автозагрузке. Если нет — добавляемся.
            RegisterInStartup();

            // 3. НАСТРОЙКА РЕЖИМА ВЫХОДА
            // Приложение не закроется, даже если нет открытых окон.
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 4. ЗАПУСК ФОНОВОЙ ЛОГИКИ
            await _host.StartAsync();

            // ВАЖНО: Мы НЕ вызываем mainWindow.Show(). Приложение запущено скрытно.
            // Окна появятся только когда сработает логика в Services.

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            using (_host)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }
            _instanceMutex?.ReleaseMutex();
            base.OnExit(e);
        }

        /// <summary>
        /// Добавляет приложение в автозагрузку текущего пользователя через Реестр.
        /// Это заменяет установку службы.
        /// </summary>
        private void RegisterInStartup()
        {
            try
            {
                // Путь к текущему exe файлу
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;

                // Открываем ключ реестра автозагрузки
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    // Проверяем, записан ли уже правильный путь
                    var existingValue = key.GetValue(AppName) as string;
                    if (existingValue != exePath)
                    {
                        key.SetValue(AppName, exePath);
                    }
                }
            }
            catch (Exception ex)
            {
                // Если нет прав или ошибка, можно записать в лог, но обычно для HKCU права есть.
                Debug.WriteLine($"Ошибка автозагрузки: {ex.Message}");
            }
        }
    }
}