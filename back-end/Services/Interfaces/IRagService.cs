namespace Services.Interfaces;

public interface IRagService
{
    Task<ChatResponseDto> ProcessQueryAsync(string sessionId, string userId, string prompt);
}

public class ChatResponseDto
{
    public string Answer { get; set; } = string.Empty;
    public bool IsExternal { get; set; }
    public double SimilarityScore { get; set; }
    public string ResolutionType { get; set; } = string.Empty;
    public double CustoEstimadoUsd { get; set; }
}
