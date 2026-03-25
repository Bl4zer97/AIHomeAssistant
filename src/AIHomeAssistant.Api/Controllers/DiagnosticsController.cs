using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AIHomeAssistant.Api.Controllers;

/// <summary>
/// Diagnostics endpoint: exposes recent command log (Story 2.4).
/// GET /api/diagnostics/command-log — returns the last N commands.
/// Intended for local network access only; not exposed externally.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly ICommandRepository _commandLog;

    public DiagnosticsController(ICommandRepository commandLog)
        => _commandLog = commandLog;

    /// <summary>Returns the last 50 command log entries.</summary>
    [HttpGet("command-log")]
    [ProducesResponseType(typeof(IEnumerable<CommandRecord>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCommandLog(
        [FromQuery] int count = 50,
        CancellationToken ct = default)
    {
        if (count is < 1 or > 500) count = 50;
        var entries = await _commandLog.GetRecentAsync(count, ct);
        return Ok(entries);
    }
}
