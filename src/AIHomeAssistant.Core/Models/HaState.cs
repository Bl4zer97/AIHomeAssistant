namespace AIHomeAssistant.Core.Models;

public record HaState(
    string EntityId,
    string State,
    IReadOnlyDictionary<string, object?> Attributes,
    DateTimeOffset LastUpdated
);
