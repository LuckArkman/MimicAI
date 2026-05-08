using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repositorys.Interfaces;
using Services.Implementations;

namespace WorkerService.BackgroundServices;

public class GalileuWorker : BackgroundService
{
    private readonly ILogger<GalileuWorker> _logger;
    private readonly IVectorRepository _vectorRepository;
    private readonly ChannelReader<IngestionTask> _channelReader;

    public GalileuWorker(
        ILogger<GalileuWorker> logger,
        IVectorRepository vectorRepository,
        Channel<IngestionTask> channel)
    {
        _logger = logger;
        _vectorRepository = vectorRepository;
        _channelReader = channel.Reader;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=== AGENTE GALILEU DE IA INICIADO E OPERANDO EM SEGUNDO PLANO ===");

        // Ensure vector collection exists at startup
        try
        {
            await _vectorRepository.EnsureCollectionExistsAsync("mimic_ai_memory");
            _logger.LogInformation("Coleção vetorial 'mimic_ai_memory' inicializada/verificada no ChromaDB.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Falha ao inicializar coleção no ChromaDB: {ex.Message}. Certifique-se de que o ChromaDB está rodando.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for a new ingestion task in the channel
                if (await _channelReader.WaitToReadAsync(stoppingToken))
                {
                    while (_channelReader.TryRead(out var task))
                    {
                        _logger.LogInformation($"[GALILEU AGENT] Nova interação capturada. Iniciando vetorização e tratamento de contexto...");

                        // 1. Process prompt and remote answer into a single document memory
                        string documentContent = $"Pergunta: {task.Prompt}\nResposta: {task.Answer}";
                        string documentId = Guid.NewGuid().ToString("N");

                        // 2. Generate vector embedding using local embedding generator
                        float[] embedding = GenerateLocalEmbedding(documentContent);

                        // 3. Storing metadata
                        var metadata = new Dictionary<string, object>
                        {
                            { "prompt", task.Prompt },
                            { "created_at", DateTime.UtcNow.ToString("O") },
                            { "source", "external_learning" }
                        };

                        // 4. Save inside ChromaDB
                        bool success = await _vectorRepository.InsertVectorAsync(documentId, embedding, documentContent, metadata);

                        if (success)
                        {
                            _logger.LogInformation($"[GALILEU AGENT SUCCESS] Interação '{documentId}' vetorizada e armazenada no ChromaDB. Conhecimento local expandido!");
                        }
                        else
                        {
                            _logger.LogWarning($"[GALILEU AGENT ERROR] Falha ao gravar vetor no ChromaDB. Certifique-se de que o container do ChromaDB está online.");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Agente Galileu desligando graciosamente...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[GALILEU AGENT EXCEPTION] Erro crítico no loop do background worker: {ex.Message}");
                await Task.Delay(5000, stoppingToken); // delay to prevent rapid looping on error
            }
        }
    }

    private float[] GenerateLocalEmbedding(string text)
    {
        // Deterministic 384-dimensional vectorizer matching the RagService calculation
        float[] vector = new float[384];
        if (string.IsNullOrEmpty(text)) return vector;

        string normalized = text.ToLower();
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < vector.Length; i++)
        {
            double sum = 0;
            foreach (var word in words)
            {
                int hash = word.GetHashCode();
                sum += Math.Sin(hash + i) * Math.Cos((double)i / vector.Length);
            }
            vector[i] = (float)Math.Clamp(sum / (words.Length > 0 ? words.Length : 1), -1.0, 1.0);
        }

        // Normalize
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
