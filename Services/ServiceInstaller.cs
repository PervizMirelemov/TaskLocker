using System;
using System.Diagnostics;
using System.Reflection;
using System.Linq;

namespace TaskLocker.Service
{
    /// <summary>
    /// Утилитарный класс, отвечающий за установку, удаление и управление жизненным циклом 
    /// Службы Windows с помощью 'sc.exe'.
    /// </summary>
    public static class ServiceInstaller
    {
        // --- КОНСТАНТЫ СЛУЖБЫ ---
        private const string SERVICE_NAME = "TaskLockerService";
        private const string DISPLAY_NAME = "Task Locker Background Service";
        // -------------------------

        /// <summary>
        /// Устанавливает и запускает Службу Windows. Должен быть вызван с правами администратора.
        /// </summary>
        public static void Install()
        {
            try
            {
                // Получаем полный путь к exe файлу службы
                string exePath = Assembly.GetExecutingAssembly().Location;

                Console.WriteLine($"[ServiceInstaller] Installing service: {SERVICE_NAME} at {exePath}...");

                // 1. Создаем службу (start=auto - для автоматического запуска при загрузке)
                // ВАЖНО: binPath= и DisplayName= должны иметь пробел после знака равенства!
                RunSc($"create \"{SERVICE_NAME}\" binPath= \"{exePath}\" start= auto DisplayName= \"{DISPLAY_NAME}\"");

                // 2. Настраиваем восстановление (перезапуск через 60 секунд при сбое)
                RunSc($"failure \"{SERVICE_NAME}\" reset= 0 actions= restart/60000/restart/60000/restart/60000");

                // 3. Запускаем службу
                RunSc($"start \"{SERVICE_NAME}\"");
                Console.WriteLine($"[ServiceInstaller] Service '{SERVICE_NAME}' successfully installed and started.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ServiceInstaller] Installation error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Останавливает и удаляет Службу Windows.
        /// </summary>
        public static void Uninstall()
        {
            try
            {
                Console.WriteLine($"[ServiceInstaller] Stopping and deleting service: {SERVICE_NAME}...");

                // 1. Останавливаем службу (1062 - ожидаемый код, если служба уже остановлена)
                RunSc($"stop \"{SERVICE_NAME}\"");

                // 2. Удаляем службу (1060 - ожидаемый код, если службы не существует)
                RunSc($"delete \"{SERVICE_NAME}\"");

                Console.WriteLine($"[ServiceInstaller] Service '{SERVICE_NAME}' successfully uninstalled.");
            }
            catch (Exception ex)
            {
                // Игнорируем ошибки при удалении, если служба не существует (1060) или уже остановлена (1062)
                if (!ex.Message.Contains("1060") && !ex.Message.Contains("1062"))
                {
                    Console.WriteLine($"[ServiceInstaller] Uninstall error (ignored): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Выполняет команду с помощью утилиты sc.exe (Service Control).
        /// </summary>
        /// <param name="arguments">Аргументы для передачи sc.exe.</param>
        private static void RunSc(string arguments)
        {
            var process = new Process();
            process.StartInfo.FileName = "sc.exe";
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true; // Скрываем черное окно
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Коды выхода 0 (успех), 1062 (не запущена), 1060 (не существует) считаются нормальными
            if (process.ExitCode != 0 && process.ExitCode != 1062 && process.ExitCode != 1060)
            {
                throw new InvalidOperationException($"SC command failed: sc {arguments}. Exit Code: {process.ExitCode}. Output: {output}");
            }
        }
    }
}