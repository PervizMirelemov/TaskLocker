using System.Windows;

namespace TaskLocker.WPF.Views
{
    public partial class LockOverlayWindow : Window
    {
        public LockOverlayWindow()
        {
            InitializeComponent();
            this.Loaded += LockOverlayWindow_Loaded;
            this.Deactivated += LockOverlayWindow_Deactivated;
        }

        private void LockOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Activate();
            this.Focus();
        }

        private void LockOverlayWindow_Deactivated(object? sender, System.EventArgs e)
        {
            this.Topmost = true;
            this.Activate();
        }

        // НОВЫЙ МЕТОД: Обновление текста на экране
        public void UpdateTimer(int secondsLeft)
        {
            CountdownText.Text = $"Автоматическая разблокировка через {secondsLeft} сек...";
        }
    }
}