using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

/// <summary>Persists and queries command log records.</summary>
public interface ICommandRepository
{
    Task InsertAsync(CommandRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<CommandRecord>> GetRecentAsync(int count = 20, CancellationToken ct = default);
}
