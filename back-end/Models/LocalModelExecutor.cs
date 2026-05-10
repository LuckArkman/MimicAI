using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Models;

/// <summary>
/// Executa inferência nativa em C# utilizando o modelo ONNX pré-compilado.
/// Esta classe é a responsável pela inteligência local autônoma e generalização das respostas,
/// completamente isolada no projeto class library de Models conforme a arquitetura.
/// </summary>
public class LocalModelExecutor : IDisposable
{
    private readonly InferenceSession _session;
    private readonly Tokenizer _tokenizer;

    public LocalModelExecutor()
    {
        // Os arquivos devem ser resolvidos no subdiretório 'Onnx' a partir do diretório base
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var modelDir = Path.Combine(baseDir, "Onnx");
        
        var modelPath = Path.Combine(modelDir, "gpt2.onnx");
        var vocabPath = Path.Combine(modelDir, "vocab.json");
        var mergesPath = Path.Combine(modelDir, "merges.txt");

        if (!File.Exists(modelPath) || !File.Exists(vocabPath) || !File.Exists(mergesPath))
        {
            throw new FileNotFoundException($"Os arquivos do modelo ONNX não foram encontrados no diretório '{modelDir}'. Certifique-se de que os arquivos foram baixados e estão marcados para serem copiados no Models.csproj.");
        }

        _tokenizer = new Tokenizer(new Bpe(vocabPath, mergesPath));
        
        var options = new SessionOptions();
        options.AppendExecutionProvider_CPU();
        _session = new InferenceSession(modelPath, options);
    }

    /// <summary>
    /// Realiza a geração autoregressiva da resposta do modelo local.
    /// </summary>
    public async Task<string> GenerateResponseAsync(string prompt, string context = "")
    {
        // Utilizamos Task.Run para mover a carga pesada de CPU bound para uma thread de pool de trabalho
        return await Task.Run(() => GenerateResponse(prompt, context));
    }

    private string GenerateResponse(string prompt, string context)
    {
        string formattedPrompt = string.IsNullOrEmpty(context)
            ? $"Question: {prompt}\nAnswer:"
            : $"Context: {context}\nQuestion: {prompt}\nAnswer:";

        var tokenIdsList = _tokenizer.EncodeToIds(formattedPrompt).Select(x => (long)x).ToList();
        
        // GPT-2 has a maximum context window of 1024 tokens.
        // Truncate the input if it exceeds 990 tokens to ensure we have room for generation.
        if (tokenIdsList.Count > 990)
        {
            tokenIdsList = tokenIdsList.TakeLast(990).ToList();
        }
        
        int promptTokenCount = tokenIdsList.Count;

        int maxTokensToGenerate = 30;
        for (int step = 0; step < maxTokensToGenerate; step++)
        {
            if (tokenIdsList.Count >= 1024)
            {
                break;
            }

            long[] inputIds = tokenIdsList.ToArray();
            int[] shape = { 1, inputIds.Length };

            var inputTensor = new DenseTensor<long>(inputIds, shape);
            
            // Create attention_mask tensor (all ones, shape 1 x sequence_length)
            long[] maskIds = Enumerable.Repeat(1L, inputIds.Length).ToArray();
            var maskTensor = new DenseTensor<long>(maskIds, shape);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor)
            };

            using var outputs = _session.Run(inputs);
            var logitsValue = outputs.First(v => v.Name == "logits").AsTensor<float>();

            int seqLength = (int)logitsValue.Dimensions[1];
            int vocabSize = (int)logitsValue.Dimensions[2];
            int lastTokenOffset = (seqLength - 1) * vocabSize;

            int nextTokenId = 0;
            float maxLogitValue = float.MinValue;
            for (int v = 0; v < vocabSize; v++)
            {
                float logit = logitsValue.GetValue(lastTokenOffset + v);
                if (logit > maxLogitValue)
                {
                    maxLogitValue = logit;
                    nextTokenId = v;
                }
            }

            // Para quando encontra o token EOS (50256) ou passa do limite de contexto (1024)
            if (nextTokenId == 50256 || tokenIdsList.Count >= 1023)
            {
                break;
            }

            tokenIdsList.Add(nextTokenId);
        }

        var generatedTokens = tokenIdsList.Skip(promptTokenCount).Select(x => (int)x).ToList();
        string? answer = _tokenizer.Decode(generatedTokens);

        return answer?.Trim() ?? string.Empty;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
