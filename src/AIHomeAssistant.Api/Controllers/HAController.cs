using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AIHomeAssistant.Api.Controllers;

/// <summary>
/// Home-Assistant alert management endpoints (Story 3.x — Phase 2).
/// GET    /api/ha/alerts                      — list active alerts
/// GET    /api/ha/alerts/{id}                 — get single alert
/// POST   /api/ha/alerts/{id}/acknowledge     — acknowledge an alert
/// POST   /api/ha/alerts/{id}/clear           — clear/resolve an alert
/// </summary>
[ApiController]
[Route("api/ha/alerts")]
public class HAController : ControllerBase
{
    private readonly IAlertRepository _alerts;
    private readonly INotificationService _notify;
    private readonly ILogger<HAController> _logger;

    public HAController(
        IAlertRepository alerts,
        INotificationService notify,
        ILogger<HAController> logger)
    {
        _alerts = alerts;
        _notify = notify;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AlertState>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveAlerts(CancellationToken ct)
        => Ok(await _alerts.GetActiveAlertsAsync(ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AlertState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAlert(int id, CancellationToken ct)
    {
        var alert = await _alerts.GetByIdAsync(id, ct);
        return alert is null ? NotFound() : Ok(alert);
    }

    [HttpPost("{id:int}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Acknowledge(int id, CancellationToken ct)
    {
        var alert = await _alerts.GetByIdAsync(id, ct);
        if (alert is null) return NotFound();

        await _alerts.AcknowledgeAsync(id, ct);
        _logger.LogInformation("Alert {Id} acknowledged", id);
        return NoContent();
    }

    [HttpPost("{id:int}/clear")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Clear(int id, CancellationToken ct)
    {
        var alert = await _alerts.GetByIdAsync(id, ct);
        if (alert is null) return NotFound();

        await _alerts.ClearAsync(id, ct);
        _logger.LogInformation("Alert {Id} cleared", id);
        return NoContent();
    }

    /// <summary>Create a new alert (called from monitoring scripts or HA automations).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AlertState), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAlert([FromBody] CreateAlertRequest req, CancellationToken ct)
    {
        var saved = await _alerts.CreateAlertAsync(req.SensorId, ct);

        // Fire-and-forget Telegram notification
        _ = _notify.SendAsync($"🚨 New alert: sensor {req.SensorId} triggered", ct);

        return CreatedAtAction(nameof(GetAlert), new { id = saved.Id }, saved);
    }
}

public record CreateAlertRequest(string SensorId);
