using System.Collections.Concurrent;
using System.Threading.Tasks;
using Services.Interfaces;

namespace Services.Implementations;

public class FinetuningService : IFinetuningService
{
    private static readonly ConcurrentDictionary<string, FinetuningJobDto> _jobs = new();

    public Task<FinetuningJobDto> StartFinetuningJobAsync(string model, string datasetSource)
    {
        var jobId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var job = new FinetuningJobDto
        {
            JobId = jobId,
            ModelName = model,
            DatasetSource = datasetSource,
            Status = "Queued",
            TotalEpochs = 5,
            CurrentEpoch = 0,
            TrainingLoss = 2.5,
            ValidationLoss = 2.8,
            ProgressPercentage = 0,
            CreatedAt = DateTime.UtcNow
        };

        _jobs[jobId] = job;

        // Simulate local alignment/fine-tuning loop asynchronously
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000); // Wait in queue
            job.Status = "Running";

            for (int epoch = 1; epoch <= job.TotalEpochs; epoch++)
            {
                if (job.Status != "Running") break;

                job.CurrentEpoch = epoch;
                job.ProgressPercentage = (epoch / (double)job.TotalEpochs) * 100;
                
                // Simulate progressive loss improvement
                job.TrainingLoss = Math.Round(2.5 * Math.Exp(-0.6 * epoch) + 0.1, 4);
                job.ValidationLoss = Math.Round(2.8 * Math.Exp(-0.55 * epoch) + 0.15, 4);

                await Task.Delay(4000); // 4 seconds per epoch simulation
            }

            job.Status = "Completed";
            job.CompletedAt = DateTime.UtcNow;
            job.ProgressPercentage = 100;
        });

        return Task.FromResult(job);
    }

    public Task<FinetuningJobDto?> GetJobStatusAsync(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<List<FinetuningJobDto>> GetAllJobsAsync()
    {
        var list = _jobs.Values.OrderByDescending(j => j.CreatedAt).ToList();
        return Task.FromResult(list);
    }
}
