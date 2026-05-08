namespace Services.Interfaces;

public interface IOrchestrationService
{
    Task<AgentStatusDto> GetAgentStatusAsync();
    Task<bool> TriggerManualLearningAsync(string prompt, string response);
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
