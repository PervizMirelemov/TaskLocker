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
        private void Lock()
        {
            // actually lock the workstation
            _windowService.LockWorkStation();
        }
    }
}