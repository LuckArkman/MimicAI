using System.Threading.Tasks;

namespace Services.Interfaces;

public interface ILocalLlmService
{
    Task<string> GenerateResponseAsync(string prompt, string context = "");
}
