using System.Threading.Channels;
using Database.Chroma;
using Services.Interfaces;

namespace Services.Implementations;

public class OrchestrationService : IOrchestrationService
{
    private readonly Channel<IngestionTask> _channel;
    private readonly ChromaDbContext _chromaContext;
    private static int _manuallyProcessedCount = 0;

    public OrchestrationService(Channel<IngestionTask> channel, ChromaDbContext chromaContext)
    {
        _channel = channel;
        _chromaContext = chromaContext;
    }

    public async Task<AgentStatusDto> GetAgentStatusAsync()
    {
        bool chromaConnected = await _chromaContext.CheckHeartbeatAsync();
        
        // Estimate process task count based on our static tracker and whatever is in queue
        int pending = _channel.Reader.Count;

        return new AgentStatusDto
        {
            AgentName = "Agente Galileu",
            Status = "Operando",
            PendingQueueCount = pending,
            ProcessedTasksCount = _manuallyProcessedCount,
            ChromaDbCollection = "mimic_ai_memory",
            ChromaDbConnected = chromaConnected,
            CosineSimilarityThreshold = 0.78
        };
    }

    public async Task<bool> TriggerManualLearningAsync(string prompt, string response)
    {
        if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(response))
            return false;

        var task = new IngestionTask(prompt, response);
        await _channel.Writer.WriteAsync(task);
        
        Interlocked.Increment(ref _manuallyProcessedCount);
        return true;
    }
}
