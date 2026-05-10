using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FinetuningController : ControllerBase
{
    private readonly IFinetuningService _finetuningService;

    public FinetuningController(IFinetuningService finetuningService)
    {
        _finetuningService = finetuningService;
    }

    [HttpPost("job")]
    public async Task<IActionResult> StartJob([FromBody] StartFinetuningRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ModelName))
        {
            return BadRequest(new { message = "O nome do modelo a ser alinhado é obrigatório." });
        }

        var job = await _finetuningService.StartFinetuningJobAsync(request.ModelName, request.DatasetSource);
        return Ok(job);
    }

    [HttpGet("job/{jobId}")]
    public async Task<IActionResult> GetJob(string jobId)
    {
        var job = await _finetuningService.GetJobStatusAsync(jobId);
        if (job == null)
        {
            return NotFound(new { message = $"Job '{jobId}' não encontrado." });
        }
        return Ok(job);
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetAllJobs()
    {
        var jobs = await _finetuningService.GetAllJobsAsync();
        return Ok(jobs);
    }
}

public record StartFinetuningRequest(string ModelName, string DatasetSource);
