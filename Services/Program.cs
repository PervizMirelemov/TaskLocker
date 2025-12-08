using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using TaskLocker.Service; // !!! Ссылка на наш новый класс ServiceInstaller
using TaskLocker.WPF.Services; // Ваш Namespace для сервисов
using TaskLocker.WPF.Native; // Ваш Namespace для NativeMethods

// --- ТОЧКА ВХОДА ПРИЛОЖЕНИЯ ---

// 1. Проверяем аргументы: если программа запущена установщиком, 
// вызываем ServiceInstaller и завершаем работу.
if (args.Contains("--install"))
{
    ServiceInstaller.Install();
    return;
}
if (args.Contains("--uninstall"))
{
    ServiceInstaller.Uninstall();
    return;
}

// 2. Если нет аргументов установки, запускаем приложение как фоновую службу Windows.
IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        // Имя службы должно совпадать с константой SERVICE_NAME в ServiceInstaller
        options.ServiceName = "TaskLockerService";
    })
    .ConfigureServices(services =>
    {
        // --- Регистрация вашей фоновой логики ---

        // Регистрируем основную фоновую задачу
        services.AddHostedService<ShutdownDialogMonitorService>();

        // Регистрируем зависимости, которые ей нужны
        services.AddSingleton<PInvokeWindowService>();
        services.AddSingleton<IWindowManagementService>(s => s.GetRequiredService<PInvokeWindowService>());
    })
    .Build();

await host.RunAsync();