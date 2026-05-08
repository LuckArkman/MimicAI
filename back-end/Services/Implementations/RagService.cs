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
        // 1. Generate local embedding vector for the prompt (delegated to BERT/MiniLM Agent)
        float[] queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(prompt);

        // 2. Perform semantic search in local ChromaDB
        var vectorMatch = await _vectorRepository.SearchSimilarVectorsAsync(queryEmbedding, limit: 1);

        // 3. Evaluate context sufficiency
        if (vectorMatch != null && vectorMatch.SimilarityScore >= _cosineThreshold)
        {
            // CACHE HIT - local model digestion (delegated to LocalLlmService)
            string context = vectorMatch.DocumentContent;
            string localAnswer = await _localLlmService.GenerateResponseAsync(prompt, context);

            // Store message in MongoDB
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
        var externalResult = await _externalLlm.GenerateResponseAsync(prompt);

        // Store message in MongoDB
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

        // 4. Enqueue background ingestion task to Worker Service (Galileu Agent) for learning
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
