using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace TaskLocker.WPF.Services
{
    public class ShutdownDialogMonitorService : IHostedService, IDisposable
    {
        // one-shot timer; schedule next run manually so we can guarantee "5 seconds after close"
        private const int TimerIntervalSeconds = 5;
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
            // fire once immediately; after each callback we reschedule for TimerIntervalSeconds
            _timer = new Timer(DoWork, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
            return Task.CompletedTask;
        }

        private void DoWork(object? state)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1) return;
            try
            {
                _logger.LogInformation("Monitor tick: checking shutdown dialog...");

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    dispatcher.Invoke(() =>
                    {
                        if (!_windowService.IsShutdownDialogVisible())
                        {
                            _logger.LogWarning("Shutdown dialog not visible — forcing show.");
                            _windowService.ShowShutdownDialog();
                        }
                        else
                        {
                            _logger.LogInformation("Shutdown dialog already visible.");
                        }
                    });
                }
                else
                {
                    if (!_windowService.IsShutdownDialogVisible())
                    {
                        _logger.LogWarning("Shutdown dialog not visible — forcing show.");
                        _windowService.ShowShutdownDialog();
                    }
                    else
                    {
                        _logger.LogInformation("Shutdown dialog already visible.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during shutdown dialog monitor tick.");
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
                // schedule next run exactly TimerIntervalSeconds after this callback completes (i.e. after modal closes)
                try
                {
                    _timer?.Change(TimeSpan.FromSeconds(TimerIntervalSeconds), Timeout.InfiniteTimeSpan);
                }
                catch (ObjectDisposedException)
                {
                    // ignore if timer disposed during shutdown
                }
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