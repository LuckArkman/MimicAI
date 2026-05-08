using MongoDB.Driver;

namespace Database.Mongo;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<ChatSessionDocument> ChatSessions => 
        _database.GetCollection<ChatSessionDocument>("ChatSessions");

    public IMongoCollection<ChatMessageDocument> ChatMessages => 
        _database.GetCollection<ChatMessageDocument>("ChatMessages");
}

public class ChatSessionDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = "Nova Conversa";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ChatMessageDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty; // "User" or "AI"
    public string Content { get; set; } = string.Empty;
    public bool IsExternal { get; set; } // True if escalated, False if hit local
    public double SimilarityScore { get; set; } // Score returned from ChromaDB
    public string ResolutionType { get; set; } = string.Empty; // "Local (Hit)", "External (Fallback)"
    public double CustoEstimado { get; set; } // Custo estimado em USD se for API externa
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
