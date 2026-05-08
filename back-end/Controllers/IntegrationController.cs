using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class IntegrationController : ControllerBase
{
    private readonly IIntegrationService _integrationService;

    public IntegrationController(IIntegrationService integrationService)
    {
        _integrationService = integrationService;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        var health = await _integrationService.CheckHealthAsync();
        return Ok(health);
    }

    [HttpPost("test-external")]
    public async Task<IActionResult> TestExternalConnection()
    {
        var result = await _integrationService.TestExternalLlmConnectionAsync();
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
