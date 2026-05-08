using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Services.Interfaces;

namespace Services.Implementations;

public class LocalLlmService : ILocalLlmService
{
    private readonly HttpClient _httpClient;
    private readonly string _ollamaUrl = "http://localhost:11434/api/generate";
    private readonly string _localModel = "gemma2"; // standard local LLM model

    public LocalLlmService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var configuredUrl = configuration["Ollama:Url"];
        if (!string.IsNullOrEmpty(configuredUrl))
        {
            _ollamaUrl = configuredUrl;
        }
        var configuredModel = configuration["Ollama:Model"];
        if (!string.IsNullOrEmpty(configuredModel))
        {
            _localModel = configuredModel;
        }
    }

    public async Task<string> GenerateResponseAsync(string prompt, string context = "")
    {
        try
        {
            // Build the system instructions and include RAG context
            var localPrompt = string.IsNullOrEmpty(context)
                ? prompt
                : $"[CONTEXTO RECUPERADO]\n{context}\n\n[PERGUNTA]\n{prompt}\n\nResponda com base no contexto acima de forma sucinta e objetiva:";

            var payload = new
            {
                model = _localModel,
                prompt = localPrompt,
                stream = false
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_ollamaUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;
                if (root.TryGetProperty("response", out var respProp))
                {
                    return respProp.GetString() ?? string.Empty;
                }
            }

            return $"[LOCAL INFERENCE ERROR] Falha ao comunicar com Ollama (Status: {response.StatusCode}).";
        }
        catch (Exception ex)
        {
            return $"[LOCAL INFERENCE FALLBACK] Modelo local Ollama indisponível ({ex.Message}).";
        }
    }
}
