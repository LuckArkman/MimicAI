using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrchestrationController : ControllerBase
{
    private readonly IOrchestrationService _orchestrationService;

    public OrchestrationController(IOrchestrationService orchestrationService)
    {
        _orchestrationService = orchestrationService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await _orchestrationService.GetAgentStatusAsync();
        return Ok(status);
    }

    [HttpPost("trigger-learning")]
    public async Task<IActionResult> TriggerLearning([FromBody] ManualLearningRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt) || string.IsNullOrWhiteSpace(request.Response))
        {
            return BadRequest(new { message = "Os campos 'prompt' e 'response' são obrigatórios para a ingestão." });
        }

        bool success = await _orchestrationService.TriggerManualLearningAsync(request.Prompt, request.Response);
        if (!success)
        {
            return BadRequest(new { message = "Falha ao enfileirar tarefa de aprendizado." });
        }

        return Ok(new { message = "Interação enviada com sucesso para a fila do agente Galileu!" });
    }

    [HttpPost("ingest-parquet")]
    public async Task<IActionResult> IngestParquet([FromBody] ParquetIngestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return BadRequest(new { message = "O campo 'filePath' é obrigatório para a importação." });
        }

        var result = await _orchestrationService.IngestParquetFileAsync(request.FilePath);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}

public record ManualLearningRequest(string Prompt, string Response);
public record ParquetIngestionRequest(string FilePath);
