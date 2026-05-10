using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Models;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>
/// Serviço de inferência local que age como uma ponte (facade) entre as requisições do sistema e
/// a biblioteca de classe Models, que detém a execução nativa e encapsulada do ONNX.
/// </summary>
public class LocalLlmService : ILocalLlmService
{
    private readonly LocalModelExecutor _modelExecutor;

    public LocalLlmService(LocalModelExecutor modelExecutor)
    {
        _modelExecutor = modelExecutor;
    }

    /// <summary>
    /// Gera respostas curtas de forma local realizando inferência no modelo encapsulado,
    /// com base no prompt e no contexto do ChromaDB.
    /// </summary>
    public async Task<string> GenerateResponseAsync(string prompt, string context = "")
    {
        // Se houver um contexto estruturado com "Resposta:", extraímos a resposta de alta qualidade diretamente.
        // Isso garante que o cache semântico retorne uma resposta perfeita em português, sem repetições.
        if (!string.IsNullOrEmpty(context))
        {
            var lines = context.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var answerLine = lines.FirstOrDefault(l => l.StartsWith("Resposta:"));
            if (answerLine != null)
            {
                return answerLine.Substring("Resposta:".Length).Trim();
            }
        }

        if (_modelExecutor == null)
        {
            return GetSimulatedFallback(prompt, context);
        }

        try
        {
            var answer = await _modelExecutor.GenerateResponseAsync(prompt, context);
            
            if (string.IsNullOrWhiteSpace(answer))
            {
                return GetSimulatedFallback(prompt, context);
            }

            return answer;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GPT-2 ONNX RUNTIME FALLBACK] {ex.Message}");
            return GetSimulatedFallback(prompt, context);
        }
    }

    /// <summary>
    /// Gerador de fallback que simula as respostas caso o motor ONNX não consiga inicializar,
    /// ideal para tolerância a falhas na pipeline RAG.
    /// </summary>
    private string GetSimulatedFallback(string prompt, string context)
    {
        if (string.IsNullOrEmpty(context))
        {
            return $"[GPT-2 ONNX OFFLINE] Baseado no prompt '{prompt}', sugiro analisar as opções no painel ou acionar o fallback externo.";
        }

        var lines = context.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var keyLine = lines.FirstOrDefault(l => l.StartsWith("Resposta:"))
                      ?? lines.FirstOrDefault(l => l.Contains(":") || l.Length > 20)
                      ?? lines.First();

        string cleanLine = keyLine;
        if (cleanLine.StartsWith("Resposta:"))
        {
            cleanLine = cleanLine.Substring("Resposta:".Length).Trim();
        }

        return cleanLine;
    }
}
