namespace AIHomeAssistant.Core.Models;

public record ConsentRecord(
    int Id,
    string MemberId,
    string MemberName,
    string AzurePersonId,
    DateTimeOffset GrantedAt,
    DateTimeOffset? RevokedAt,
    bool Active
);
