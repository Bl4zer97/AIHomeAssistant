namespace AIHomeAssistant.Core.Models;

public record CommandRecord(
    int Id,
    DateTimeOffset CreatedAt,
    string? Transcript,
    string? ResolvedIntent,
    string? EntityId,
    int? HaResponseCode,
    int? LatencyMs,
    string? AudioProfile,
    string? ErrorCode
);
