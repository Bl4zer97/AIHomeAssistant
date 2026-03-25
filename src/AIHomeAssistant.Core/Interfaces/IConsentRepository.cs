using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Core.Interfaces;

/// <summary>Face recognition consent management (GDPR Art.9).</summary>
public interface IConsentRepository
{
    Task<ConsentRecord> GrantConsentAsync(string memberId, string memberName, string azurePersonId, CancellationToken ct = default);
    Task RevokeConsentAsync(string memberId, CancellationToken ct = default);
    Task<ConsentRecord?> GetConsentAsync(string memberId, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentRecord>> GetAllActiveConsentsAsync(CancellationToken ct = default);
}
