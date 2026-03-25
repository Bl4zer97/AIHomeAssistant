using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

public interface IHomeAssistantClient
{
    Task<PipelineResult<HaState>> GetStateAsync(string entityId, CancellationToken ct = default);
    Task<PipelineResult<IReadOnlyList<HaState>>> GetAllStatesAsync(CancellationToken ct = default);
    /// <summary>Calls a Home Assistant service action (e.g. light.turn_on).</summary>
    Task<PipelineResult> CallServiceAsync(
        string domain, string service, object? payload = null, CancellationToken ct = default);
}

