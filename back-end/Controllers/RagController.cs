using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositorys.Interfaces;
using Services.Interfaces;

namespace Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    private readonly IRagService _ragService;
    private readonly IChatHistoryRepository _chatHistoryRepository;

    public RagController(IRagService ragService, IChatHistoryRepository chatHistoryRepository)
    {
        _ragService = ragService;
        _chatHistoryRepository = chatHistoryRepository;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { message = "O prompt não pode ser vazio." });
        }

        string sessionId = request.SessionId;
        if (string.IsNullOrEmpty(sessionId))
        {
            // If no session ID provided, create a new session
            var newSession = await _chatHistoryRepository.CreateSessionAsync(request.UserId, "Nova Conversa");
            sessionId = newSession.Id;
        }

        var response = await _ragService.ProcessQueryAsync(sessionId, request.UserId, request.Prompt);
        return Ok(new
        {
            sessionId = sessionId,
            answer = response.Answer,
            isExternal = response.IsExternal,
            similarityScore = response.SimilarityScore,
            resolutionType = response.ResolutionType,
            custoEstimadoUsd = response.CustoEstimadoUsd
        });
    }

    [HttpGet("sessions/{userId}")]
    public async Task<IActionResult> GetSessions(string userId)
    {
        var sessions = await _chatHistoryRepository.GetSessionsByUserIdAsync(userId);
        return Ok(sessions);
    }

    [HttpPost("session")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        var session = await _chatHistoryRepository.CreateSessionAsync(request.UserId, request.Title);
        return Ok(session);
    }

    [HttpGet("session/{sessionId}/messages")]
    public async Task<IActionResult> GetMessages(string sessionId)
    {
        var messages = await _chatHistoryRepository.GetMessagesBySessionIdAsync(sessionId);
        return Ok(messages);
    }

    [HttpGet("metrics/{userId}")]
    public async Task<IActionResult> GetMetrics(string userId)
    {
        var metrics = await _chatHistoryRepository.GetMetricsSummaryAsync(userId);
        return Ok(metrics);
    }
}

public record ChatRequest(string UserId, string SessionId, string Prompt);
public record CreateSessionRequest(string UserId, string Title);
