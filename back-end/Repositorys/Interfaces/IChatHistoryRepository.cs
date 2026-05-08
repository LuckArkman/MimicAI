using Database.Mongo;

namespace Repositorys.Interfaces;

public interface IChatHistoryRepository
{
    Task<ChatSessionDocument?> GetSessionByIdAsync(string sessionId);
    Task<List<ChatSessionDocument>> GetSessionsByUserIdAsync(string userId);
    Task<ChatSessionDocument> CreateSessionAsync(string userId, string title);
    Task<List<ChatMessageDocument>> GetMessagesBySessionIdAsync(string sessionId);
    Task SaveMessageAsync(ChatMessageDocument message);
    Task<MetricsSummary> GetMetricsSummaryAsync(string userId);
}

public class MetricsSummary
{
    public int TotalLocalHits { get; set; }
    public int TotalExternalEscalations { get; set; }
    public double TotalSavingsUsd { get; set; }
    public double TotalCostsUsd { get; set; }
    public double AutonomyRate { get; set; } // Percentage of local hits / total
}
