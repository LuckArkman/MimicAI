namespace Services.Interfaces;

public interface IExternalLlmService
{
    Task<LlmResult> GenerateResponseAsync(string prompt, string systemMessage = "");
}

public class LlmResult
{
    public string Content { get; set; } = string.Empty;
    public double EstimatedCostUsd { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
}
