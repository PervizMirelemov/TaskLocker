using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;   // Добавлено для работы с файлами
// Явно указываем таймер
using Timer = System.Threading.Timer;

namespace TaskLocker.WPF.Services
{
    public class ShutdownDialogMonitorService : IHostedService, IDisposable
    {
        private const int DefaultIntervalSeconds = 5;

        // ПУТЬ К ФАЙЛУ С ПОЛЬЗОВАТЕЛЯМИ
        // Укажите здесь реальный путь к вашему txt файлу
        private const string UserListFilePath = @"\\fc1-1c-app01\1C_Exchange\PopupWindow.txt";

        private readonly IWindowManagementService _windowService;
        private readonly ILogger<ShutdownDialogMonitorService> _logger;
        private Timer? _timer;
        private int _running;

        public ShutdownDialogMonitorService(IWindowManagementService windowService, ILogger<ShutdownDialogMonitorService> logger)
        {
            _windowService = windowService;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting ShutdownDialogMonitorService.");
            _timer = new Timer(DoWork, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
            return Task.CompletedTask;
        }

        private void DoWork(object? state)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1) return;
            try
            {
                // 1. ПРОВЕРКА: Разрешено ли этому пользователю видеть окно?
                if (IsUserAllowed())
                {
                    // Если пользователь в списке — работаем как обычно
                    if (!_windowService.IsShutdownDialogVisible())
                    {
                        _windowService.ShowShutdownDialog();
                    }
                }
                else
                {
                    // Если пользователя нет в списке — ничего не делаем, просто ждем следующего тика
                    _logger.LogDebug("User not in allowed list. Skipping dialog show.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during shutdown dialog monitor tick.");
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);

                TimeSpan delay;
                // Если была установлена задержка (например, 20 минут)
                if (_windowService.NextShowDelay > TimeSpan.Zero)
                {
                    delay = _windowService.NextShowDelay;
                    _windowService.NextShowDelay = TimeSpan.Zero;
                }
                else
                {
                    delay = TimeSpan.FromSeconds(DefaultIntervalSeconds);
                }

                try
                {
                    _timer?.Change(delay, Timeout.InfiniteTimeSpan);
                }
                catch (ObjectDisposedException) { }
            }
        }

        // НОВАЯ ФУНКЦИЯ: Проверка пользователя по файлу
        private bool IsUserAllowed()
        {
            try
            {
                // Если файла нет — считаем, что блокировка НЕ должна работать (или создайте пустой файл)
                if (!File.Exists(UserListFilePath))
                {
                    // Можно раскомментировать лог, если нужно знать об отсутствии файла
                    // _logger.LogWarning($"User list file not found at {UserListFilePath}");
                    return false;
                }

                // Читаем файл, убираем пустые строки и пробелы
                var allowedUsers = File.ReadAllLines(UserListFilePath)
                                       .Select(line => line.Trim())
                                       .Where(line => !string.IsNullOrEmpty(line));

                string currentUser = Environment.UserName;

                // Проверяем наличие текущего пользователя в списке (без учета регистра букв)
                return allowedUsers.Contains(currentUser, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read allowed users file.");
                return false; // В случае ошибки файла — не блокируем
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping ShutdownDialogMonitorService.");
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}