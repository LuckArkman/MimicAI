using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repositorys.Interfaces;
using Services.Implementations;
using Services.Interfaces;

namespace WorkerService.BackgroundServices;

/// <summary>
/// Worker de segundo plano assíncrono (Agente Galileu de IA).
/// Consome tarefas de vetorização e as indexa no banco de dados vetorial ChromaDB de forma não-bloqueante.
/// </summary>
public class GalileuWorker : BackgroundService
{
    private readonly ILogger<GalileuWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ChannelReader<IngestionTask> _channelReader;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="GalileuWorker"/>.
    /// </summary>
    /// <param name="logger">O logger para logs de segundo plano.</param>
    /// <param name="serviceScopeFactory">A fábrica para criação de escopos e resolução de dependências Scoped.</param>
    /// <param name="channel">O canal concorrente de tarefas de ingestão.</param>
    public GalileuWorker(
        ILogger<GalileuWorker> logger,
        IServiceScopeFactory serviceScopeFactory,
        Channel<IngestionTask> channel)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _channelReader = channel.Reader;
    }

    /// <summary>
    /// Executa o processamento contínuo das tarefas em segundo plano.
    /// </summary>
    /// <param name="stoppingToken">O token de cancelamento de desligamento do Host.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=== AGENTE GALILEU DE IA INICIADO E OPERANDO EM SEGUNDO PLANO ===");

        // Assegura que a coleção vetorial existe na inicialização usando um escopo temporário
        try
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var vectorRepository = scope.ServiceProvider.GetRequiredService<IVectorRepository>();
                await vectorRepository.EnsureCollectionExistsAsync("mimic_ai_memory");
            }
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
                // Aguarda por novas tarefas de ingestão no canal
                if (await _channelReader.WaitToReadAsync(stoppingToken))
                {
                    while (_channelReader.TryRead(out var task))
                    {
                        _logger.LogInformation($"[GALILEU AGENT] Nova interação capturada. Iniciando vetorização e tratamento de contexto...");

                        // 1. Prepara o conteúdo unificado da interação
                        string documentContent = $"Pergunta: {task.Prompt}\nResposta: {task.Answer}";
                        string documentId = Guid.NewGuid().ToString("N");

                        // 2. Computa o embedding local usando o agente BERT unificado
                        float[] embedding;
                        using (var scope = _serviceScopeFactory.CreateScope())
                        {
                            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
                            embedding = await embeddingService.GenerateEmbeddingAsync(documentContent);
                        }

                        // 3. Estrutura os metadados
                        var metadata = new Dictionary<string, object>
                        {
                            { "prompt", task.Prompt },
                            { "created_at", DateTime.UtcNow.ToString("O") },
                            { "source", "external_learning" }
                        };

                        // 4. Salva no banco de dados vetorial resolvendo o repositório em um escopo próprio
                        bool success = false;
                        using (var scope = _serviceScopeFactory.CreateScope())
                        {
                            var vectorRepository = scope.ServiceProvider.GetRequiredService<IVectorRepository>();
                            success = await vectorRepository.InsertVectorAsync(documentId, embedding, documentContent, metadata);
                        }

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
}
