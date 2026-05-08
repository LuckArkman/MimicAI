namespace Services.Interfaces;

public interface IFinetuningService
{
    Task<FinetuningJobDto> StartFinetuningJobAsync(string model, string datasetSource);
    Task<FinetuningJobDto?> GetJobStatusAsync(string jobId);
    Task<List<FinetuningJobDto>> GetAllJobsAsync();
}

public class FinetuningJobDto
{
    public string JobId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string DatasetSource { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Queued", "Running", "Completed", "Failed"
    public int TotalEpochs { get; set; }
    public int CurrentEpoch { get; set; }
    public double TrainingLoss { get; set; }
    public double ValidationLoss { get; set; }
    public double ProgressPercentage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
