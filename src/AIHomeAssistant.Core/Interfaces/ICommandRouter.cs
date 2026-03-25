using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

/// <summary>Routes a resolved intent to the appropriate handler.</summary>
public interface ICommandRouter
{
    Task<PipelineResult<string?>> RouteAsync(IntentResult intent, CancellationToken ct = default);
}
