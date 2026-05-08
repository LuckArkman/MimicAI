using System.Net.Http;
using System.Text;
using System.Text.Json;
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
    private readonly HttpClient _httpClient;
    private readonly ChannelWriter<IngestionTask> _channelWriter;
    private readonly double _cosineThreshold = 0.78; // Decision Threshold
    private readonly string _localLlmUrl = "http://localhost:11434/api/generate"; // Default Ollama URL
    private readonly string _localModel = "gemma2"; // Default local model

    public RagService(
        IVectorRepository vectorRepository,
        IChatHistoryRepository chatHistoryRepository,
        IExternalLlmService externalLlm,
        HttpClient httpClient,
        Channel<IngestionTask> channel)
    {
        _vectorRepository = vectorRepository;
        _chatHistoryRepository = chatHistoryRepository;
        _externalLlm = externalLlm;
        _httpClient = httpClient;
        _channelWriter = channel.Writer;
    }

    public async Task<ChatResponseDto> ProcessQueryAsync(string sessionId, string userId, string prompt)
    {
        // 1. Generate local embedding vector for the prompt
        float[] queryEmbedding = GenerateLocalEmbedding(prompt);

        // 2. Perform semantic search in local ChromaDB
        var vectorMatch = await _vectorRepository.SearchSimilarVectorsAsync(queryEmbedding, limit: 1);

        // 3. Evaluate context sufficiency
        if (vectorMatch != null && vectorMatch.SimilarityScore >= _cosineThreshold)
        {
            // CACHE HIT - local model digestion
            string context = vectorMatch.DocumentContent;
            string localAnswer = await ExecuteLocalInferenceAsync(prompt, context);

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

        // CACHE MISS - escalate to external LLM (Gemini)
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

    private async Task<string> ExecuteLocalInferenceAsync(string prompt, string context)
    {
        try
        {
            // Inject context into the prompt
            var localPrompt = $"[CONTEXTO RECUPERADO]\n{context}\n\n[PERGUNTA]\n{prompt}\n\nResponda de forma sucinta com base no contexto acima:";

            var payload = new
            {
                model = _localModel,
                prompt = localPrompt,
                stream = false
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_localLlmUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                return $"[LOCAL INFERENCE FALLBACK] Encontrei contexto local, mas falhei ao conectar ao modelo local Ollama. Contexto recuperado: '{context}'";
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            return $"[LOCAL INFERENCE EXCEPTION] Encontrei contexto local, mas o modelo local (Ollama) está offline. Erro: {ex.Message}. Contexto recuperado: '{context}'";
        }
    }

    private float[] GenerateLocalEmbedding(string text)
    {
        // Lightweight deterministic hashing vectorizer for testing / offline usage.
        // It outputs a 384-dimensional float array (same as all-MiniLM-L6-v2)
        float[] vector = new float[384];
        if (string.IsNullOrEmpty(text)) return vector;

        string normalized = text.ToLower();
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < vector.Length; i++)
        {
            double sum = 0;
            foreach (var word in words)
            {
                // Simple deterministic sine/cosine hashing based on word index and word value hash
                int hash = word.GetHashCode();
                sum += Math.Sin(hash + i) * Math.Cos((double)i / vector.Length);
            }
            vector[i] = (float)Math.Clamp(sum / (words.Length > 0 ? words.Length : 1), -1.0, 1.0);
        }

        // Normalize vector to unit length (L2 normalization)
        double normSum = 0;
        for (int i = 0; i < vector.Length; i++)
            normSum += vector[i] * vector[i];

        double norm = Math.Sqrt(normSum);
        if (norm > 0)
        {
            for (int i = 0; i < vector.Length; i++)
                vector[i] = (float)(vector[i] / norm);
        }

        return vector;
    }
}

public record IngestionTask(string Prompt, string Answer);
