using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskLocker.WPF.Services;

namespace TaskLocker.WPF.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IWindowManagementService _windowService;
        private readonly IApiService _apiService;

        public MainViewModel(IWindowManagementService windowService, IApiService apiService)
        {
            _windowService = windowService;
            _apiService = apiService;
        }

        [RelayCommand]
        private void Yes()
        {
            // Логика ДА: Скрыть окно и показать через 1 минуту
            // Устанавливаем задержку 1 минута (60 секунд)
            _windowService.NextShowDelay = TimeSpan.FromSeconds(20);

            // Закрываем текущее окно
            _windowService.HideShutdownDialog();
        }

        [RelayCommand]
        private void No()
        {
            // Логика НЕТ: Блокировать систему
            _windowService.LockWorkStation();

            // И закрываем окно
            _windowService.HideShutdownDialog();
        }

        [RelayCommand]
        private async Task SendReport()
        {
            try
            {
                await _apiService.SendLogAsync("User triggered SendReport");
                var status = await _apiService.GetStatusAsync();
                // Для простоты: логика отображения/обработки результата
            }
            catch
            {
                // Handle/log errors appropriately (logger can be injected if needed)
            }
        }
    }
}