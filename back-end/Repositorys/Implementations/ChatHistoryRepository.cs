using MongoDB.Driver;
using Database.Mongo;
using Repositorys.Interfaces;

namespace Repositorys.Implementations;

public class ChatHistoryRepository : IChatHistoryRepository
{
    private readonly MongoDbContext _context;

    public ChatHistoryRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<ChatSessionDocument?> GetSessionByIdAsync(string sessionId)
    {
        return await _context.ChatSessions.Find(s => s.Id == sessionId).FirstOrDefaultAsync();
    }

    public async Task<List<ChatSessionDocument>> GetSessionsByUserIdAsync(string userId)
    {
        return await _context.ChatSessions.Find(s => s.UserId == userId).SortByDescending(s => s.UpdatedAt).ToListAsync();
    }

    public async Task<ChatSessionDocument> CreateSessionAsync(string userId, string title)
    {
        var session = new ChatSessionDocument
        {
            UserId = userId,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.ChatSessions.InsertOneAsync(session);
        return session;
    }

    public async Task<List<ChatMessageDocument>> GetMessagesBySessionIdAsync(string sessionId)
    {
        return await _context.ChatMessages.Find(m => m.SessionId == sessionId).SortBy(m => m.Timestamp).ToListAsync();
    }

    public async Task SaveMessageAsync(ChatMessageDocument message)
    {
        await _context.ChatMessages.InsertOneAsync(message);
        
        // Update session's UpdatedAt field
        var update = Builders<ChatSessionDocument>.Update.Set(s => s.UpdatedAt, DateTime.UtcNow);
        await _context.ChatSessions.UpdateOneAsync(s => s.Id == message.SessionId, update);
    }

    public async Task<MetricsSummary> GetMetricsSummaryAsync(string userId)
    {
        var filter = Builders<ChatMessageDocument>.Filter.Eq(m => m.UserId, userId);
        var messages = await _context.ChatMessages.Find(filter).ToListAsync();

        int localHits = messages.Count(m => !m.IsExternal && m.Sender == "AI");
        int externalEscalations = messages.Count(m => m.IsExternal && m.Sender == "AI");
        double totalCost = messages.Where(m => m.IsExternal && m.Sender == "AI").Sum(m => m.CustoEstimado);

        // Cada chamada externa de alta capacidade custa aproximadamente 0.015 USD
        // Cada chamada local custa 0 USD (economia de 0.015 USD por hit local)
        double estimatedSavingRate = 0.015;
        double savings = localHits * estimatedSavingRate;

        int totalAIResponses = localHits + externalEscalations;
        double autonomy = totalAIResponses > 0 ? ((double)localHits / totalAIResponses) * 100 : 100.0;

        return new MetricsSummary
        {
            TotalLocalHits = localHits,
            TotalExternalEscalations = externalEscalations,
            TotalCostsUsd = totalCost,
            TotalSavingsUsd = savings,
            AutonomyRate = Math.Round(autonomy, 1)
        };
    }
}
