using System.Threading.Channels;
using Database.Mongo;
using Repositorys.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

public class RagService : IRagService
{
    private readonly IVectorRepository _vectorRepository;
    private readonly IChatHistoryRepository _chatHistoryRepository;
    private readonly IExternalLlmService _externalLlm;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILocalLlmService _localLlmService;
    private readonly ChannelWriter<IngestionTask> _channelWriter;
    private readonly double _cosineThreshold = 0.78; // Decision Threshold

    public RagService(
        IVectorRepository vectorRepository,
        IChatHistoryRepository chatHistoryRepository,
        IExternalLlmService externalLlm,
        IEmbeddingService embeddingService,
        ILocalLlmService localLlmService,
        Channel<IngestionTask> channel)
    {
        _vectorRepository = vectorRepository;
        _chatHistoryRepository = chatHistoryRepository;
        _externalLlm = externalLlm;
        _embeddingService = embeddingService;
        _localLlmService = localLlmService;
        _channelWriter = channel.Writer;
    }

    public async Task<ChatResponseDto> ProcessQueryAsync(string sessionId, string userId, string prompt)
    {
        // 0. Save User prompt message to MongoDB to establish conversational thread history
        var userMsg = new ChatMessageDocument
        {
            SessionId = sessionId,
            UserId = userId,
            Sender = "user",
            Content = prompt,
            IsExternal = false,
            SimilarityScore = 0.0,
            ResolutionType = "user",
            CustoEstimado = 0.0,
            Timestamp = DateTime.UtcNow
        };
        await _chatHistoryRepository.SaveMessageAsync(userMsg);

        // 1. Extract recent conversation history (MCP Context preservation)
        var previousMessages = await _chatHistoryRepository.GetMessagesBySessionIdAsync(sessionId);
        var historyBuilder = new System.Text.StringBuilder();
        if (previousMessages != null && previousMessages.Any())
        {
            // Fetch last 10 messages to fit model context windows comfortably
            var lastMessages = previousMessages.OrderBy(m => m.Timestamp).TakeLast(10);
            foreach (var msg in lastMessages)
            {
                string senderName = msg.Sender == "user" ? "Usuário" : "Agente Galileu";
                historyBuilder.AppendLine($"{senderName}: {msg.Content}");
            }
        }
        string conversationHistory = historyBuilder.ToString();

        // 2. Generate local embedding vector for the prompt (delegated to BERT/MiniLM Agent)
        float[] queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(prompt);

        // 3. Perform semantic search in local ChromaDB
        var vectorMatch = await _vectorRepository.SearchSimilarVectorsAsync(queryEmbedding, limit: 1);

        // 4. Evaluate context sufficiency
        if (vectorMatch != null && vectorMatch.SimilarityScore >= _cosineThreshold)
        {
            // CACHE HIT - local model digestion (delegated to LocalLlmService)
            string context = vectorMatch.DocumentContent;
            
            // Prepend conversation history to local model to maintain context (MCP)
            string localPrompt = prompt;
            if (!string.IsNullOrEmpty(conversationHistory))
            {
                localPrompt = $"Histórico da Conversa:\n{conversationHistory}\nPergunta: {prompt}";
            }

            string localAnswer = await _localLlmService.GenerateResponseAsync(localPrompt, context);

            // Store AI response message in MongoDB
            var chatMsg = new ChatMessageDocument
            {
                SessionId = sessionId,
                UserId = userId,
                Sender = "AI",
                Content = localAnswer,
                IsExternal = false,
                SimilarityScore = vectorMatch.SimilarityScore,
                ResolutionType = "Local (Hit)",
                CustoEstimado = 0.0,
                Timestamp = DateTime.UtcNow
            };
            await _chatHistoryRepository.SaveMessageAsync(chatMsg);

            return new ChatResponseDto
            {
                Answer = localAnswer,
                IsExternal = false,
                SimilarityScore = vectorMatch.SimilarityScore,
                ResolutionType = "Local (Hit)",
                CustoEstimadoUsd = 0.0
            };
        }

        // CACHE MISS - escalate to external LLM (Gemini API)
        // Feed conversational context as a system prompt to Gemini to preserve continuity (MCP)
        string systemMessage = "Você é o Agente Galileu, uma inteligência artificial local de triagem corporativa inteligente. Use o histórico recente para manter o contexto da conversa:\n" + conversationHistory;
        var externalResult = await _externalLlm.GenerateResponseAsync(prompt, systemMessage);

        // Store AI response message in MongoDB
        var externalMsg = new ChatMessageDocument
        {
            SessionId = sessionId,
            UserId = userId,
            Sender = "AI",
            Content = externalResult.Content,
            IsExternal = true,
            SimilarityScore = vectorMatch?.SimilarityScore ?? 0.0,
            ResolutionType = "External (Fallback)",
            CustoEstimado = externalResult.EstimatedCostUsd,
            Timestamp = DateTime.UtcNow
        };
        await _chatHistoryRepository.SaveMessageAsync(externalMsg);

        // Enqueue background ingestion task to Worker Service (Galileu Agent) for learning
        await _channelWriter.WriteAsync(new IngestionTask(prompt, externalResult.Content));

        return new ChatResponseDto
        {
            Answer = externalResult.Content,
            IsExternal = true,
            SimilarityScore = vectorMatch?.SimilarityScore ?? 0.0,
            ResolutionType = "External (Fallback)",
            CustoEstimadoUsd = externalResult.EstimatedCostUsd
        };
    }
}

public record IngestionTask(string Prompt, string Answer);
