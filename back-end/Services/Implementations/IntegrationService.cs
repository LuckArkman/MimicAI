using System.Net.Http;
using Database.Chroma;
using Database.Mongo;
using Database.Postgres;
using Services.Interfaces;

namespace Services.Implementations;

public class IntegrationService : IIntegrationService
{
    private readonly AppDbContext _appDbContext;
    private readonly MongoDbContext _mongoDbContext;
    private readonly ChromaDbContext _chromaContext;
    private readonly IExternalLlmService _externalLlm;
    private readonly HttpClient _httpClient;

    public IntegrationService(
        AppDbContext appDbContext,
        MongoDbContext mongoDbContext,
        ChromaDbContext chromaContext,
        IExternalLlmService externalLlm,
        HttpClient httpClient)
    {
        _appDbContext = appDbContext;
        _mongoDbContext = mongoDbContext;
        _chromaContext = chromaContext;
        _externalLlm = externalLlm;
        _httpClient = httpClient;
    }

    public async Task<HealthCheckResultDto> CheckHealthAsync()
    {
        var result = new HealthCheckResultDto();
        bool allHealthy = true;

        // 1. PostgreSQL check
        try
        {
            bool postgresOk = await _appDbContext.Database.CanConnectAsync();
            result.PostgresStatus = postgresOk ? "Connected" : "Disconnected";
            if (!postgresOk) allHealthy = false;
        }
        catch (Exception ex)
        {
            result.PostgresStatus = $"Exception: {ex.Message}";
            allHealthy = false;
        }

        // 2. MongoDB check
        try
        {
            // Simple operation to assert connection
            var count = await _mongoDbContext.ChatSessions.CountDocumentsAsync(MongoDB.Driver.Builders<ChatSessionDocument>.Filter.Empty);
            result.MongoDbStatus = "Connected";
        }
        catch (Exception ex)
        {
            result.MongoDbStatus = $"Exception: {ex.Message}";
            allHealthy = false;
        }

        // 3. ChromaDB check
        try
        {
            bool chromaOk = await _chromaContext.CheckHeartbeatAsync();
            result.ChromaDbStatus = chromaOk ? "Connected" : "Disconnected";
            if (!chromaOk) allHealthy = false;
        }
        catch (Exception ex)
        {
            result.ChromaDbStatus = $"Exception: {ex.Message}";
            allHealthy = false;
        }

        // 4. Ollama Local LLM check (port 11434 default)
        try
        {
            var response = await _httpClient.GetAsync("http://localhost:11434");
            result.OllamaStatus = response.IsSuccessStatusCode ? "Online" : "Offline";
        }
        catch (Exception ex)
        {
            result.OllamaStatus = $"Offline (Exception: {ex.Message})";
            allHealthy = false;
        }

        result.Healthy = allHealthy;
        result.Message = allHealthy 
            ? "Todos os sistemas operando de forma ideal." 
            : "Atenção: um ou mais serviços estão inacessíveis.";

        return result;
    }

    public async Task<ExternalLlmTestResultDto> TestExternalLlmConnectionAsync()
    {
        var testPrompt = "Olá! Isto é um teste de integridade da API externa do MIMIC AI. Responda apenas com a palavra: 'INTEGRIDADE_OK'.";
        var result = await _externalLlm.GenerateResponseAsync(testPrompt);

        bool success = result.Content.Contains("INTEGRIDADE_OK") || (!result.Content.StartsWith("[") && !result.Content.Contains("ERROR"));

        return new ExternalLlmTestResultDto
        {
            Success = success,
            ResponseText = result.Content,
            EstimatedCostUsd = result.EstimatedCostUsd,
            ErrorDetails = success ? string.Empty : "Verifique as configurações da API externa no appsettings.json."
        };
    }
}
