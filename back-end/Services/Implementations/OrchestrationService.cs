using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using Database.Chroma;
using Services.Interfaces;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

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

    public async Task<ParquetIngestionResultDto> IngestParquetFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new ParquetIngestionResultDto
            {
                Success = false,
                ImportedCount = 0,
                Message = "O caminho do arquivo Parquet não foi informado."
            };
        }

        string resolvedPath = filePath;

        // Map and normalize Windows paths (e.g. "I:\Galileu.Node\...") to Linux container paths (e.g. "/Galileu.Node/...")
        string normalized = filePath.Replace('\\', '/');
        if (normalized.Length >= 2 && normalized[1] == ':')
        {
            normalized = normalized.Substring(2);
        }

        if (!normalized.StartsWith("/"))
        {
            normalized = "/" + normalized;
        }

        if (File.Exists(normalized))
        {
            resolvedPath = normalized;
        }
        else if (!File.Exists(resolvedPath))
        {
            return new ParquetIngestionResultDto
            {
                Success = false,
                ImportedCount = 0,
                Message = $"O arquivo informado em '{filePath}' não existe ou não está acessível pelo container do backend. Tentamos também o caminho mapeado de container '{normalized}', mas ele também não existe."
            };
        }

        try
        {
            int importedCount = 0;

            using (var fs = File.OpenRead(resolvedPath))
            {
                await using (var reader = await ParquetReader.CreateAsync(fs))
                {
                    var schema = reader.Schema;
                    int promptIdx = -1;
                    int responseIdx = -1;

                    for (int i = 0; i < schema.DataFields.Length; i++)
                    {
                        string name = schema.DataFields[i].Name.ToLower();
                        if (name == "prompt") promptIdx = i;
                        if (name == "response" || name == "resposta" || name == "answer") responseIdx = i;
                    }

                    if (promptIdx == -1 || responseIdx == -1)
                    {
                        return new ParquetIngestionResultDto
                        {
                            Success = false,
                            ImportedCount = 0,
                            Message = "O arquivo Parquet deve conter pelo menos uma coluna chamada 'prompt' e outra chamada 'response' (ou 'resposta'/'answer')."
                        };
                    }

                    for (int i = 0; i < reader.RowGroupCount; i++)
                    {
                        using (var groupReader = reader.OpenRowGroupReader(i))
                        {
                            int rowCount = (int)groupReader.RowCount;
                            var promptBuffer = new string?[rowCount];
                            var responseBuffer = new string?[rowCount];

                            await groupReader.ReadAsync(schema.DataFields[promptIdx], promptBuffer);
                            await groupReader.ReadAsync(schema.DataFields[responseIdx], responseBuffer);

                            for (int r = 0; r < rowCount; r++)
                            {
                                string promptVal = promptBuffer[r] ?? string.Empty;
                                string responseVal = responseBuffer[r] ?? string.Empty;

                                if (!string.IsNullOrWhiteSpace(promptVal) && !string.IsNullOrWhiteSpace(responseVal))
                                {
                                    var task = new IngestionTask(promptVal, responseVal);
                                    await _channel.Writer.WriteAsync(task);
                                    
                                    Interlocked.Increment(ref _manuallyProcessedCount);
                                    importedCount++;
                                }
                            }
                        }
                    }
                }
            }

            return new ParquetIngestionResultDto
            {
                Success = true,
                ImportedCount = importedCount,
                Message = $"Sucesso! Foram extraídos {importedCount} pares de prompt-resposta do arquivo Parquet e enfileirados para vetorização sem bloqueio."
            };
        }
        catch (Exception ex)
        {
            return new ParquetIngestionResultDto
            {
                Success = false,
                ImportedCount = 0,
                Message = $"Falha ao ler ou desserializar o arquivo Parquet: {ex.Message}"
            };
        }
    }
}
