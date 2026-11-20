using MetroMesh.Identification.Review.Contracts;

namespace MetroMesh.Identification.Review.Persistence;

public interface IIdentificationStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<bool> TryMarkProcessedAsync(string eventId, CancellationToken cancellationToken = default);
    Task RecordInteractionAsync(CustomerStationInteraction interaction, CancellationToken cancellationToken = default);
    Task<CustomerStationInteraction?> GetInteractionAsync(string interactionId, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
    Task RecordAlertAsync(AlertNotification alert, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertNotification>> GetAlertsAsync(string? interactionId, CancellationToken cancellationToken = default);
    Task RecordAuditAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntry>> GetAuditAsync(string? interactionId, CancellationToken cancellationToken = default);
    Task RecordReviewAsync(ReviewDecision decision, CancellationToken cancellationToken = default);
    Task<ReviewDecision?> GetReviewAsync(string interactionId, CancellationToken cancellationToken = default);
    Task<int> PurgeNonMatchOlderThanAsync(DateTimeOffset olderThan, string reason, CancellationToken cancellationToken = default);
}
