namespace Services.Interfaces;

public interface IOrchestrationService
{
    Task<AgentStatusDto> GetAgentStatusAsync();
    Task<bool> TriggerManualLearningAsync(string prompt, string response);
    Task<ParquetIngestionResultDto> IngestParquetFileAsync(string filePath);
}

public class AgentStatusDto
{
    public string AgentName { get; set; } = "Agente Galileu";
    public string Status { get; set; } = "Operando"; // "Operando", "Pausado", "Desconectado"
    public int PendingQueueCount { get; set; }
    public int ProcessedTasksCount { get; set; }
    public string ChromaDbCollection { get; set; } = "mimic_ai_memory";
    public bool ChromaDbConnected { get; set; }
    public double CosineSimilarityThreshold { get; set; }
}

public class ParquetIngestionResultDto
{
    public bool Success { get; set; }
    public int ImportedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
