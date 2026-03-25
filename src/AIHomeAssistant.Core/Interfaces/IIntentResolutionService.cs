using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

/// <summary>Resolves natural language transcripts to structured device intents.</summary>
public interface IIntentResolutionService
{
    Task<PipelineResult<IntentResult>> ResolveIntentAsync(
        string transcript,
        IReadOnlyList<HaState> haContext,
        IReadOnlyList<string>? sessionHistory = null,
        CancellationToken ct = default);
}
