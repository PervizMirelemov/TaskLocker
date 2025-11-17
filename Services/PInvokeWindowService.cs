using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using TaskLocker.WPF.Native;

namespace TaskLocker.WPF.Services
{
    public class PInvokeWindowService : IWindowManagementService
    {
        private const string DialogClassName = "#32770";
        private const string WindowTitle_RU = "Завершение работы Windows";
        private const string WindowTitle_EN = "Shut Down Windows";

        private readonly ILogger<PInvokeWindowService> _logger;

        // fallback WPF dialog reference (used when OS dialog not found)
        private Window? _fallbackDialog;
        private readonly object _dialogLock = new();

        public PInvokeWindowService(ILogger<PInvokeWindowService> logger)
        {
            _logger = logger;
        }

        private IntPtr FindShutdownWindowHandle()
        {
            IntPtr hWnd = NativeMethods.FindWindowW(DialogClassName, WindowTitle_RU);
            if (hWnd != IntPtr.Zero)
            {
                _logger.LogTrace("Found shutdown dialog (RU) {Handle}", hWnd);
                return hWnd;
            }

            hWnd = NativeMethods.FindWindowW(DialogClassName, WindowTitle_EN);
            if (hWnd != IntPtr.Zero)
            {
                _logger.LogTrace("Found shutdown dialog (EN) {Handle}", hWnd);
                return hWnd;
            }

            _logger.LogDebug("Shutdown dialog not found by class/title.");
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

            // fallback: show an application modal WPF dialog so the monitor can re-open it later
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
                _fallbackDialog = new MainWindow();
                var owner = Application.Current?.MainWindow;
                if (owner != null && owner != _fallbackDialog) _fallbackDialog.Owner = owner;
                // ShowDialog blocks until user closes the modal; monitor schedules next show after the callback completes
                _fallbackDialog.ShowDialog();
                _fallbackDialog = null;
            }
        }

        public void HideShutdownDialog()
        {
            IntPtr hWnd = FindShutdownWindowHandle();
            if (hWnd != IntPtr.Zero)
            {
                _logger.LogInformation("Hiding shutdown dialog (Handle: {Handle})", hWnd);
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() =>
                {
                    lock (_dialogLock)
                    {
                        _fallbackDialog?.Close();
                        _fallbackDialog = null;
                    }
                });
            }
            else
            {
                lock (_dialogLock)
                {
                    _fallbackDialog?.Close();
                    _fallbackDialog = null;
                }
            }
        }

        public bool IsShutdownDialogVisible()
        {
            IntPtr hWnd = FindShutdownWindowHandle();
            if (hWnd != IntPtr.Zero)
            {
                bool visible = NativeMethods.IsWindowVisible(hWnd);
                _logger.LogTrace("Shutdown dialog visibility: {Visible} (Handle: {Handle})", visible, hWnd);
                return visible;
            }

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