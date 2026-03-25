using AIHomeAssistant.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIHomeAssistant.Api.Controllers;

/// <summary>
/// Camera snapshot endpoint (Story 4.1 — Phase 3).
/// GET /api/camera/snapshot?sensorId={sensorId} — capture and analyse a frame.
/// POST /api/camera/identify                     — identify person from uploaded image bytes.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CameraController : ControllerBase
{
    private readonly IVisionService _vision;
    private readonly IConsentRepository _consent;
    private readonly ILogger<CameraController> _logger;

    public CameraController(
        IVisionService vision,
        IConsentRepository consent,
        ILogger<CameraController> logger)
    {
        _vision = vision;
        _consent = consent;
        _logger = logger;
    }

    /// <summary>
    /// Detects presence from an uploaded image frame.
    /// Accepts multipart/form-data with a 'frame' file field.
    /// </summary>
    [HttpPost("detect-presence")]
    [ProducesResponseType(typeof(PresenceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DetectPresence(IFormFile frame, CancellationToken ct)
    {
        if (frame is null || frame.Length == 0)
            return UnprocessableEntity(new ProblemDetails { Title = "No frame provided" });

        using var ms = new MemoryStream();
        await frame.CopyToAsync(ms, ct);
        var imageBytes = ms.ToArray();

        var result = await _vision.DetectPresenceAsync(imageBytes, ct);
        if (!result.Success)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Presence detection failed",
                Detail = result.Error?.Message,
                Extensions = { ["code"] = result.Error?.Code }
            });

        return Ok(new PresenceResult(result.Value));
    }

    /// <summary>
    /// Identifies a person from an uploaded image frame.
    /// Only runs if GDPR consent exists for the matching person.
    /// </summary>
    [HttpPost("identify")]
    [ProducesResponseType(typeof(IdentityResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> IdentifyPerson(IFormFile frame, CancellationToken ct)
    {
        if (frame is null || frame.Length == 0)
            return UnprocessableEntity(new ProblemDetails { Title = "No frame provided" });

        using var ms = new MemoryStream();
        await frame.CopyToAsync(ms, ct);
        var imageBytes = ms.ToArray();

        var result = await _vision.IdentifyPersonAsync(imageBytes, string.Empty, ct);
        if (!result.Success)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Person identification failed",
                Detail = result.Error?.Message,
                Extensions = { ["code"] = result.Error?.Code }
            });

        string? personName = result.Value;
        // If identified, verify consent before returning name (GDPR Art.9)
        if (personName is not null)
        {
            var consent = await _consent.GetConsentAsync(personName, ct);
            if (consent is null || !consent.Active)
            {
                _logger.LogWarning("Person {Name} identified but has no active consent — redacting", personName);
                personName = null;
            }
        }

        return Ok(new IdentityResult(personName));
    }
}

public record PresenceResult(bool PersonDetected);
public record IdentityResult(string? PersonName);
