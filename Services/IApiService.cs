using System.Threading.Tasks;

namespace TaskLocker.WPF.Services
{
    public interface IApiService
    {
        Task<string> GetStatusAsync();
        Task SendLogAsync(string message);
    }
}