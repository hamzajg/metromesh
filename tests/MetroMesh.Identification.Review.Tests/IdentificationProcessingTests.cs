using System.Text.Json;
using MetroMesh.Identification.Review.Contracts;
using MetroMesh.Identification.Review.Persistence;
using MetroMesh.Identification.Review.Services;
using MetroMesh.Telemetry.Core.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace MetroMesh.Identification.Review.Tests;

public class IdentificationProcessingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "Identification records processed event exactly once")]
    public async Task Identification_records_processed_event_exactly_once()
    {
        var store = new FakeIdentificationStore();
        var processor = new IdentificationEventProcessor(store, NullLogger<IdentificationEventProcessor>.Instance);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var json = EnvelopeJson(eventId, "st-1", "ts-1", new { matchResult = "confirmed", confidence = 0.95, passengerId = "p-1" });

        var marked = await processor.ProcessAsync(json);

        Assert.True(marked);
        Assert.Single(store.Interactions);
        var interaction = store.Interactions[0];
        Assert.Equal(eventId, interaction.Id);
        Assert.Equal("st-1", interaction.StationId);
        Assert.Equal("ts-1", interaction.TurnstileId);
        Assert.Equal("confirmed", interaction.MatchResult);
        Assert.Equal(0.95, interaction.Confidence);
        Assert.Equal("p-1", interaction.PassengerId);
    }

    [Fact(DisplayName = "Duplicate identification delivery does not create duplicate interaction")]
    public async Task Duplicate_identification_delivery_does_not_create_duplicate_interaction()
    {
        var store = new FakeIdentificationStore();
        var processor = new IdentificationEventProcessor(store, NullLogger<IdentificationEventProcessor>.Instance);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var json = EnvelopeJson(eventId, "st-1", "ts-1", new { matchResult = "none" });

        var first = await processor.ProcessAsync(json);
        var second = await processor.ProcessAsync(json);

        Assert.True(first);
        Assert.False(second);
        Assert.Single(store.Interactions);
    }

    [Fact(DisplayName = "Malformed envelope throws invalid operation")]
    public async Task Malformed_envelope_throws_invalid_operation()
    {
        var store = new FakeIdentificationStore();
        var processor = new IdentificationEventProcessor(store, NullLogger<IdentificationEventProcessor>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync("{ not json"));
        Assert.Contains("Malformed identification envelope", ex.Message);
    }

    [Fact(DisplayName = "Envelope missing event id throws invalid operation")]
    public async Task Envelope_missing_event_id_throws_invalid_operation()
    {
        var store = new FakeIdentificationStore();
        var processor = new IdentificationEventProcessor(store, NullLogger<IdentificationEventProcessor>.Instance);
        var json = JsonSerializer.Serialize(new { specversion = "1.0", type = "x" }, JsonOptions);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(json));
        Assert.Contains("missing an event id", ex.Message);
    }

    [Fact(DisplayName = "No match result defaults to none")]
    public async Task No_match_result_defaults_to_none()
    {
        var store = new FakeIdentificationStore();
        var processor = new IdentificationEventProcessor(store, NullLogger<IdentificationEventProcessor>.Instance);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var json = EnvelopeJson(eventId, "st-2", "ts-2", new { });

        await processor.ProcessAsync(json);

        Assert.Single(store.Interactions);
        Assert.Equal("none", store.Interactions[0].MatchResult);
        Assert.Null(store.Interactions[0].Redacted);
    }

    private static string EnvelopeJson(string eventId, string stationId, string equipmentId, object payload)
    {
        var envelope = new TelemetryEventEnvelope(
            "1.0",
            eventId,
            "urn:metromesh:identification:review.v1",
            "urn:metromesh:device:camera-1",
            "identification/camera-1",
            DateTimeOffset.UtcNow,
            "application/json",
            "corr-ident-1",
            null,
            new TelemetryEventData(
                equipmentId,
                stationId,
                "identification.passenger.identified",
                DateTimeOffset.UtcNow,
                JsonSerializer.SerializeToElement(payload)));

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    [Fact(DisplayName = "Store records alert notification for offender match")]
    public async Task Store_records_alert_notification_for_offender_match()
    {
        var store = new FakeIdentificationStore();
        var alert = new AlertNotification("alert-1", "int-1", "confirmed", "security@metromesh.local", "pending", DateTimeOffset.UtcNow);

        await store.RecordAlertAsync(alert);

        var alerts = await store.GetAlertsAsync(null);
        Assert.Single(alerts);
        Assert.Equal("int-1", alerts[0].InteractionId);
        Assert.Equal("confirmed", alerts[0].MatchResult);
    }

    [Fact(DisplayName = "Store records audit entry for review decision")]
    public async Task Store_records_audit_entry_for_review_decision()
    {
        var store = new FakeIdentificationStore();
        var entry = new AuditEntry("audit-1", "int-1", "review.approve", "reviewer-1", "Approved after human review", DateTimeOffset.UtcNow);

        await store.RecordAuditAsync(entry);

        var audit = await store.GetAuditAsync(null);
        Assert.Single(audit);
        Assert.Equal("int-1", audit[0].InteractionId);
        Assert.Equal("review.approve", audit[0].Action);
    }

    [Fact(DisplayName = "Store records and retrieves review decision")]
    public async Task Store_records_and_retrieves_review_decision()
    {
        var store = new FakeIdentificationStore();
        var decision = new ReviewDecision("int-1", "approve", "reviewer-1", DateTimeOffset.UtcNow);

        await store.RecordReviewAsync(decision);

        var review = await store.GetReviewAsync("int-1");
        Assert.NotNull(review);
        Assert.Equal("approve", review!.Decision);
        Assert.Equal("reviewer-1", review.Reviewer);
    }

    [Fact(DisplayName = "Store purges non-match interactions older than threshold")]
    public async Task Store_purges_non_match_interactions_older_than_threshold()
    {
        var store = new FakeIdentificationStore();
        var oldInteraction = new CustomerStationInteraction(
            "old-1", "st-1", "ts-1", "identification.passenger.identified", DateTimeOffset.UtcNow.AddDays(-100), "none", null, null, null, null);
        var recentInteraction = new CustomerStationInteraction(
            "new-1", "st-1", "ts-1", "identification.passenger.identified", DateTimeOffset.UtcNow.AddHours(-1), "none", null, null, null, null);

        await store.RecordInteractionAsync(oldInteraction);
        await store.RecordInteractionAsync(recentInteraction);

        var purged = await store.PurgeNonMatchOlderThanAsync(DateTimeOffset.UtcNow.AddDays(-30), "retention");

        Assert.Equal(1, purged);
        Assert.Null(await store.GetInteractionAsync("old-1"));
        Assert.NotNull(await store.GetInteractionAsync("new-1"));
    }
}
