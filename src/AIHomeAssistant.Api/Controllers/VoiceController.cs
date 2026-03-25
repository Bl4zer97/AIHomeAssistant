using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AIHomeAssistant.Api.Controllers;

/// <summary>
/// Exposes the voice pipeline for manual triggers and testing (Story 1.7).
/// In production the pipeline is driven by WakeWordHostedService.
/// POST /api/voice/command — submit raw text to bypass STT (useful for testing).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VoiceController : ControllerBase
{
    private readonly IIntentResolutionService _intent;
    private readonly ICommandRouter _router;
    private readonly IHaStateCacheService _haCache;
    private readonly ICommandRepository _commandLog;
    private readonly ILogger<VoiceController> _logger;

    public VoiceController(
        IIntentResolutionService intent,
        ICommandRouter router,
        IHaStateCacheService haCache,
        ICommandRepository commandLog,
        ILogger<VoiceController> logger)
    {
        _intent = intent;
        _router = router;
        _haCache = haCache;
        _commandLog = commandLog;
        _logger = logger;
    }

    /// <summary>Submit a pre-transcribed command (bypass STT for testing/dashboard).</summary>
    [HttpPost("command")]
    [ProducesResponseType(typeof(VoiceCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PostCommand(
        [FromBody] VoiceCommandRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Transcript))
            return UnprocessableEntity(new ProblemDetails { Title = "Transcript is required" });

        var haContext = _haCache.GetAllStates();
        var intentResult = await _intent.ResolveIntentAsync(req.Transcript, haContext, null, ct);
        if (!intentResult.Success)
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Intent resolution failed",
                Detail = intentResult.Error?.Message,
                Extensions = { ["code"] = intentResult.Error?.Code }
            });

        var routeResult = await _router.RouteAsync(intentResult.Value!, ct);
        if (!routeResult.Success)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Command execution failed",
                Detail = routeResult.Error?.Message,
                Extensions = { ["code"] = routeResult.Error?.Code }
            });

        await _commandLog.InsertAsync(new CommandRecord(
            0, DateTimeOffset.UtcNow, req.Transcript,
            intentResult.Value!.Action, intentResult.Value.EntityId,
            200, 0, "api", null), ct);

        return Ok(new VoiceCommandResponse(
            intentResult.Value!.Action,
            intentResult.Value.EntityId,
            routeResult.Value));
    }
}

public record VoiceCommandRequest(string Transcript);
public record VoiceCommandResponse(string? Action, string? EntityId, string? Result);
