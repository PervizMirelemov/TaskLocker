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

using Application = System.Windows.Application;
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

        public async void StartPseudoLock(TimeSpan duration)
        {
            _logger.LogInformation("Starting pseudo-lock for {Duration} seconds.", duration.TotalSeconds);

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            await dispatcher.InvokeAsync(async () =>
            {
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

                int secondsLeft = (int)duration.TotalSeconds;

                while (secondsLeft > 0)
                {
                    foreach (var win in _lockWindows)
                    {
                        if (win is LockOverlayWindow lockScreen)
                        {
                            lockScreen.UpdateTimer(secondsLeft);

                            // Дополнительная защита: постоянно держим окно наверху
                            lockScreen.Topmost = true;
                            lockScreen.Activate();
                        }
                    }

                    await Task.Delay(1000);
                    secondsLeft--;
                }

                // ВРЕМЯ ВЫШЛО: Разрешаем закрытие и закрываем
                foreach (var win in _lockWindows)
                {
                    if (win is LockOverlayWindow lockScreen)
                    {
                        // ВАЖНО: Разрешаем закрыть окно, иначе win.Close() тоже не сработает
                        lockScreen.AllowClose = true;
                        lockScreen.Close();
                    }
                }
                _lockWindows.Clear();
                _logger.LogInformation("Pseudo-lock finished. System unlocked.");
            });
        }

        // --- Остальной код без изменений ---

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
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                return dispatcher.Invoke(() =>
                    _dialogWindows.Any(w => w.IsVisible) || _lockWindows.Any(w => w.IsVisible));
            }
            return false;
        }

        public bool LockWorkStation()
        {
            return true;
        }

        private IntPtr FindShutdownWindowHandle()
        {
            IntPtr hWnd = NativeMethods.FindWindowW(DialogClassName, WindowTitle_RU);
            if (hWnd != IntPtr.Zero) return hWnd;
            return NativeMethods.FindWindowW(DialogClassName, WindowTitle_EN);
        }
    }
}