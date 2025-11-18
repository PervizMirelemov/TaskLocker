using System;

namespace TaskLocker.WPF.Services
{
    /// <summary>
    /// Contract for managing visibility of the system shutdown dialog.
    /// </summary>
    public interface IWindowManagementService
    {
        void ShowShutdownDialog();
        void HideShutdownDialog();
        bool IsShutdownDialogVisible();

        // New: request the OS to lock the workstation
        bool LockWorkStation();
        TimeSpan NextShowDelay { get; set; }
    }
}