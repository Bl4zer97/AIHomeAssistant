using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

/// <summary>Sends notifications to external channels (e.g., Telegram).</summary>
public interface INotificationService
{
    Task<PipelineResult> SendAsync(string message, CancellationToken ct = default);
}
