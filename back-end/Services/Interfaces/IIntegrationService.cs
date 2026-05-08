namespace Services.Interfaces;

public interface IIntegrationService
{
    Task<HealthCheckResultDto> CheckHealthAsync();
    Task<ExternalLlmTestResultDto> TestExternalLlmConnectionAsync();
}

public class HealthCheckResultDto
{
    public bool Healthy { get; set; }
    public string PostgresStatus { get; set; } = "Unknown";
    public string MongoDbStatus { get; set; } = "Unknown";
    public string ChromaDbStatus { get; set; } = "Unknown";
    public string OllamaStatus { get; set; } = "Unknown";
    public string Message { get; set; } = string.Empty;
}

public class ExternalLlmTestResultDto
{
    public bool Success { get; set; }
    public string ResponseText { get; set; } = string.Empty;
    public double EstimatedCostUsd { get; set; }
    public string ErrorDetails { get; set; } = string.Empty;
}
