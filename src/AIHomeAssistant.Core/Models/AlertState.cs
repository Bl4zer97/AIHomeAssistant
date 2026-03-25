namespace AIHomeAssistant.Core.Models;

public record AlertState(
    int Id,
    string SensorId,
    DateTimeOffset TriggeredAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ClearedAt,
    string Status,           // active | acknowledged | cleared
    bool NotificationFailed
);
