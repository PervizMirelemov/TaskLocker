using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TaskLocker.WPF.Native;
using TaskLocker.WPF.ViewModels;

namespace TaskLocker.WPF.Services
{
    public class PInvokeWindowService : IWindowManagementService
    {
        private const string DialogClassName = "#32770";
        private const string WindowTitle_RU = "Завершение работы Windows";
        private const string WindowTitle_EN = "Shut Down Windows";

        private readonly ILogger<PInvokeWindowService> _logger;
        private readonly IServiceProvider _serviceProvider;

        private Window? _fallbackDialog;
        private readonly object _dialogLock = new();

        // Реализация нового свойства (по умолчанию 0, что будет значить "стандартные 5 секунд")
        public TimeSpan NextShowDelay { get; set; } = TimeSpan.Zero;

        public PInvokeWindowService(ILogger<PInvokeWindowService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        private IntPtr FindShutdownWindowHandle()
        {
            IntPtr hWnd = NativeMethods.FindWindowW(DialogClassName, WindowTitle_RU);
            if (hWnd != IntPtr.Zero) return hWnd;

            hWnd = NativeMethods.FindWindowW(DialogClassName, WindowTitle_EN);
            if (hWnd != IntPtr.Zero) return hWnd;

            return IntPtr.Zero;
        }

        public void ShowShutdownDialog()
        {
            IntPtr hWnd = FindShutdownWindowHandle();
            if (hWnd != IntPtr.Zero)
            {
                _logger.LogInformation("Showing shutdown dialog (Handle: {Handle})", hWnd);
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                NativeMethods.SetForegroundWindow(hWnd);
                return;
            }

            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() => ShowFallbackDialogInternal());
                }
                else
                {
                    ShowFallbackDialogInternal();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing fallback shutdown dialog.");
            }
        }

        private void ShowFallbackDialogInternal()
        {
            lock (_dialogLock)
            {
                if (_fallbackDialog != null && _fallbackDialog.IsVisible) return;

                _logger.LogInformation("Showing fallback WPF shutdown dialog.");

                var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();

                _fallbackDialog = new MainWindow
                {
                    DataContext = viewModel
                };

                var owner = Application.Current?.MainWindow;
                if (owner != null && owner != _fallbackDialog && owner.IsVisible)
                {
                    _fallbackDialog.Owner = owner;
                }
                else
                {
                    Application.Current.MainWindow = _fallbackDialog;
                }

                // Показываем окно и ждем его закрытия
                _fallbackDialog.ShowDialog();

                // Ссылка очищается, логика (Lock или Snooze) уже выполнена кнопками во ViewModel
                _fallbackDialog = null;
            }
        }

        public void HideShutdownDialog()
        {
            IntPtr hWnd = FindShutdownWindowHandle();
            if (hWnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            Action closeAction = () =>
            {
                lock (_dialogLock)
                {
                    _fallbackDialog?.Close();
                    _fallbackDialog = null;
                }
            };

            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.Invoke(closeAction);
            else
                closeAction();
        }

        public bool IsShutdownDialogVisible()
        {
            IntPtr hWnd = FindShutdownWindowHandle();
            if (hWnd != IntPtr.Zero) return NativeMethods.IsWindowVisible(hWnd);

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return dispatcher.Invoke(() => _fallbackDialog != null && _fallbackDialog.IsVisible);
            }
            return _fallbackDialog != null && _fallbackDialog.IsVisible;
        }

        public bool LockWorkStation()
        {
            try
            {
                bool result = NativeMethods.LockWorkStation();
                _logger.LogInformation("LockWorkStation invoked, result: {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LockWorkStation failed.");
                return false;
            }
        }
    }
}