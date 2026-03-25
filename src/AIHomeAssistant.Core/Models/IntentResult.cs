namespace AIHomeAssistant.Core.Models;

public enum IntentType { Action, Query }

public record IntentResult(
    IntentType IntentType,
    string EntityId,
    string Action,
    Dictionary<string, object?> Parameters
);
