using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TaskLocker.WPF.Services
{
    public class ShutdownDialogMonitorService : IHostedService, IDisposable
    {
        private const int DefaultIntervalSeconds = 5;
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
                // Если окно не видно, показываем его (это блокирующий вызов для нашего WPF окна)
                if (!_windowService.IsShutdownDialogVisible())
                {
                    _windowService.ShowShutdownDialog();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during shutdown dialog monitor tick.");
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);

                // ОПРЕДЕЛЯЕМ СЛЕДУЮЩУЮ ЗАДЕРЖКУ
                TimeSpan delay;
                if (_windowService.NextShowDelay > TimeSpan.Zero)
                {
                    // Если была установлена особая задержка (нажата кнопка "Да")
                    delay = _windowService.NextShowDelay;
                    _logger.LogInformation("Snoozing dialog for {Delay}", delay);

                    // Сбрасываем обратно на стандартную для следующего раза
                    _windowService.NextShowDelay = TimeSpan.Zero;
                }
                else
                {
                    // Стандартная задержка 5 секунд
                    delay = TimeSpan.FromSeconds(DefaultIntervalSeconds);
                }

                try
                {
                    _timer?.Change(delay, Timeout.InfiniteTimeSpan);
                }
                catch (ObjectDisposedException) { }
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