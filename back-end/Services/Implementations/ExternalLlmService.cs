using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Services.Interfaces;

namespace Services.Implementations;

public class ExternalLlmService : IExternalLlmService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _modelName = "gemini-1.5-flash"; // default high-capacity remote model

    public ExternalLlmService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["ExternalLlm:ApiKey"] ?? string.Empty;
        var configuredModel = configuration["ExternalLlm:Model"];
        if (!string.IsNullOrEmpty(configuredModel))
        {
            _modelName = configuredModel;
        }
    }

    public async Task<LlmResult> GenerateResponseAsync(string prompt, string systemMessage = "")
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            return new LlmResult
            {
                Content = "[MIMIC AI SYSTEM ERROR] Chave de API externa não configurada. Por favor, configure 'ExternalLlm:ApiKey' no appsettings.json.",
                EstimatedCostUsd = 0.0
            };
        }

        try
        {
            // Gemini Developer API request structure
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                systemInstruction = string.IsNullOrEmpty(systemMessage) ? null : new
                {
                    parts = new[] { new { text = systemMessage } }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                return new LlmResult
                {
                    Content = $"[API ERROR] Falha na chamada da API externa (Status: {response.StatusCode}). Detalhes: {errorMsg}",
                    EstimatedCostUsd = 0.0
                };
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            // Extract the generated text
            var text = root.GetProperty("candidates")[0]
                           .GetProperty("content")
                           .GetProperty("parts")[0]
                           .GetProperty("text")
                           .GetString() ?? string.Empty;

            // Token count estimation for pricing (Gemin 1.5 Flash: $0.075 / 1M input tokens, $0.30 / 1M output tokens)
            // Very basic word-count multiplier as proxy (1 word ~ 1.33 tokens)
            int promptTokens = (int)(prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * 1.33);
            int completionTokens = (int)(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * 1.33);

            double inputCost = (promptTokens / 1000000.0) * 0.075;
            double outputCost = (completionTokens / 1000000.0) * 0.30;
            double totalCost = inputCost + outputCost;

            return new LlmResult
            {
                Content = text,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                EstimatedCostUsd = Math.Round(totalCost, 6)
            };
        }
        catch (Exception ex)
        {
            return new LlmResult
            {
                Content = $"[MIMIC AI SYSTEM ERROR] Ocorreu uma exceção ao conectar ao LLM Externo: {ex.Message}",
                EstimatedCostUsd = 0.0
            };
        }
    }
}
