using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskLocker.WPF.Services;

namespace TaskLocker.WPF.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IWindowManagementService _windowService;

        public MainViewModel(IWindowManagementService windowService)
        {
            _windowService = windowService;
        }

        [RelayCommand]
        private void Yes()
        {
            // Логика ДА: Скрыть окно и показать через 1 минуту
            // Устанавливаем задержку 1 минута (60 секунд)
            _windowService.NextShowDelay = TimeSpan.FromMinutes(1);

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
    }
}