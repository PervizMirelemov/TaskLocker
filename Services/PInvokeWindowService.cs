using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskLocker.WPF.Native;
using TaskLocker.WPF.ViewModels;
using TaskLocker.WPF.Views;

// Явно указываем, что Application - это WPF Application
using Application = System.Windows.Application;
// Псевдоним для WinForms
using WinForms = System.Windows.Forms;

namespace TaskLocker.WPF.Services
{
    public class PInvokeWindowService : IWindowManagementService
    {
        private const string DialogClassName = "#32770";
        private const string WindowTitle_RU = "Завершение работы Windows";
        private const string WindowTitle_EN = "Shut Down Windows";

        private readonly ILogger<PInvokeWindowService> _logger;
        private readonly IServiceProvider _serviceProvider;

        private readonly List<Window> _dialogWindows = new();
        private readonly List<Window> _lockWindows = new();

        private readonly object _dialogLock = new();
        private System.Windows.Threading.DispatcherFrame? _dispatcherFrame;

        public TimeSpan NextShowDelay { get; set; } = TimeSpan.Zero;

        public PInvokeWindowService(ILogger<PInvokeWindowService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        // --- ЛОГИКА "ПСЕВДО-БЛОКИРОВКИ" С ТАЙМЕРОМ ---
        public async void StartPseudoLock(TimeSpan duration)
        {
            _logger.LogInformation("Starting pseudo-lock for {Duration} seconds.", duration.TotalSeconds);

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            await dispatcher.InvokeAsync(async () =>
            {
                // 1. Создаем черные окна на всех мониторах
                _lockWindows.Clear();
                foreach (var screen in WinForms.Screen.AllScreens)
                {
                    var lockWin = new LockOverlayWindow();
                    lockWin.WindowStartupLocation = WindowStartupLocation.Manual;
                    lockWin.Left = screen.Bounds.Left;
                    lockWin.Top = screen.Bounds.Top;
                    lockWin.Width = screen.Bounds.Width;
                    lockWin.Height = screen.Bounds.Height;

                    lockWin.Show();
                    _lockWindows.Add(lockWin);
                }

                // 2. ЦИКЛ ОБРАТНОГО ОТСЧЕТА
                int secondsLeft = (int)duration.TotalSeconds;

                while (secondsLeft > 0)
                {
                    // Обновляем текст на всех экранах
                    foreach (var win in _lockWindows)
                    {
                        if (win is LockOverlayWindow lockScreen)
                        {
                            lockScreen.UpdateTimer(secondsLeft);
                        }
                    }

                    // Ждем 1 секунду
                    await Task.Delay(1000);
                    secondsLeft--;
                }

                // 3. Время вышло — закрываем окна
                foreach (var win in _lockWindows)
                {
                    win.Close();
                }
                _lockWindows.Clear();
                _logger.LogInformation("Pseudo-lock finished. System unlocked.");
            });
        }

        // --- ОСТАЛЬНОЙ КОД БЕЗ ИЗМЕНЕНИЙ ---

        public void ShowShutdownDialog()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
                dispatcher.Invoke(ShowFallbackDialogInternal);
        }

        private void ShowFallbackDialogInternal()
        {
            lock (_dialogLock)
            {
                if (_dialogWindows.Any(w => w.IsVisible)) return;

                _dialogWindows.Clear();
                var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                var screens = WinForms.Screen.AllScreens;

                foreach (var screen in screens)
                {
                    var window = new MainWindow
                    {
                        DataContext = viewModel,
                        WindowStartupLocation = WindowStartupLocation.Manual
                    };

                    window.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - window.Width) / 2;
                    window.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - window.Height) / 2;

                    _dialogWindows.Add(window);
                }

                foreach (var window in _dialogWindows) window.Show();

                if (_dialogWindows.Count > 0)
                {
                    _dispatcherFrame = new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.PushFrame(_dispatcherFrame);
                }

                _dispatcherFrame = null;
                _dialogWindows.Clear();
            }
        }

        public void HideShutdownDialog()
        {
            var dispatcher = Application.Current?.Dispatcher;
            Action closeAction = () =>
            {
                lock (_dialogLock)
                {
                    foreach (var window in _dialogWindows) window.Close();
                    _dialogWindows.Clear();
                    if (_dispatcherFrame != null) _dispatcherFrame.Continue = false;
                }
            };

            if (dispatcher != null) dispatcher.Invoke(closeAction);
        }

        public bool IsShutdownDialogVisible()
        {
            // Система считается "занятой", если открыт диалог ИЛИ идет блокировка
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                return dispatcher.Invoke(() =>
                    _dialogWindows.Any(w => w.IsVisible) || _lockWindows.Any(w => w.IsVisible));
            }
            return false;
        }

        // Заглушка, так как мы используем псевдо-блокировку
        public bool LockWorkStation()
        {
            return true;
        }

        // Приватный метод поиска окна (можно оставить или удалить, если не используется)
        private IntPtr FindShutdownWindowHandle()
        {
            IntPtr hWnd = NativeMethods.FindWindowW(DialogClassName, WindowTitle_RU);
            if (hWnd != IntPtr.Zero) return hWnd;
            return NativeMethods.FindWindowW(DialogClassName, WindowTitle_EN);
        }
    }
}