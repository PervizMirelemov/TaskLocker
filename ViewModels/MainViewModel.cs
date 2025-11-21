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

        // Свойство для хранения имени пользователя
        public string CurrentUserName { get; }

        public MainViewModel(IWindowManagementService windowService)
        {
            _windowService = windowService;

            // Получаем имя текущего пользователя Windows
            CurrentUserName = Environment.UserName;
        }

        [RelayCommand]
        private void Yes()
        {
            // Логика ДА: Скрыть окно и показать через 1 минуту (или другое время)
            _windowService.NextShowDelay = TimeSpan.FromMinutes(1);
            _windowService.HideShutdownDialog();
        }

        [RelayCommand]
        private void No()
        {
            // Логика НЕТ: Блокировать систему
            _windowService.LockWorkStation();
            _windowService.HideShutdownDialog();
        }

        [RelayCommand]
        private Task SendReport()
        {
            // No-op since API integration removed
            return Task.CompletedTask;
        }
    }
}