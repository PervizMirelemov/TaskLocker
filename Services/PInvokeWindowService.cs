using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using TaskLocker.WPF.Native;
using TaskLocker.WPF.ViewModels;
// Явно указываем, что Application - это WPF Application, чтобы избежать конфликтов с WinForms
using Application = System.Windows.Application;
// Псевдоним для WinForms, чтобы брать оттуда Screen
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

        private readonly List<Window> _openWindows = new();
        private readonly object _dialogLock = new();

        // Используем Frame для имитации модальности сразу нескольких окон
        private System.Windows.Threading.DispatcherFrame? _dispatcherFrame;

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
                    dispatcher.Invoke(ShowFallbackDialogInternal);
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
                if (_openWindows.Any(w => w.IsVisible)) return;

                _logger.LogInformation("Showing fallback WPF shutdown dialogs on all screens.");
                _openWindows.Clear();

                var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                var screens = WinForms.Screen.AllScreens;

                // 1. Создаем и настраиваем окна для всех мониторов
                foreach (var screen in screens)
                {
                    var window = new MainWindow
                    {
                        DataContext = viewModel,
                        WindowStartupLocation = WindowStartupLocation.Manual
                    };

                    double winWidth = window.Width;
                    double winHeight = window.Height;

                    window.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - winWidth) / 2;
                    window.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - winHeight) / 2;

                    _openWindows.Add(window);
                }

                // 2. Показываем ВСЕ окна в режиме .Show() (они все активны)
                foreach (var window in _openWindows)
                {
                    window.Show();
                }

                // 3. Входим в цикл ожидания (аналог ShowDialog, но для всех окон сразу)
                // Это останавливает выполнение кода здесь, пока _dispatcherFrame.Continue не станет false
                if (_openWindows.Count > 0)
                {
                    _dispatcherFrame = new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.PushFrame(_dispatcherFrame);
                }

                // Код продолжится здесь только после вызова HideShutdownDialog
                _dispatcherFrame = null;
                _openWindows.Clear();
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
                    // Закрываем все окна
                    foreach (var window in _openWindows)
                    {
                        window.Close();
                    }
                    _openWindows.Clear();

                    // ВАЖНО: Выходим из цикла ожидания в ShowFallbackDialogInternal
                    if (_dispatcherFrame != null)
                    {
                        _dispatcherFrame.Continue = false;
                    }
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
                return dispatcher.Invoke(() => _openWindows.Any(w => w.IsVisible));
            }
            return _openWindows.Any(w => w.IsVisible);
        }

        public bool LockWorkStation()
        {
            try
            {
                bool result = NativeMethods.LockWorkStation();
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