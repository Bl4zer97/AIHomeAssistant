using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

/// <summary>Alert state management for security sensors.</summary>
public interface IAlertRepository
{
    Task<AlertState> CreateAlertAsync(string sensorId, CancellationToken ct = default);
    Task AcknowledgeAsync(int alertId, CancellationToken ct = default);
    Task ClearAsync(int alertId, CancellationToken ct = default);
    Task<AlertState?> GetActiveAlertAsync(string sensorId, CancellationToken ct = default);
    Task<IReadOnlyList<AlertState>> GetActiveAlertsAsync(CancellationToken ct = default);
    Task<AlertState?> GetByIdAsync(int alertId, CancellationToken ct = default);
}
