using System.Text.Json;
using MetroMesh.Passenger.Loyalty.Contracts;
using MetroMesh.Passenger.Loyalty.Persistence;
using MetroMesh.Passenger.Loyalty.Services;
using MetroMesh.Telemetry.Core.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace MetroMesh.Passenger.Loyalty.Tests;

public class PassengerProcessingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "Passenger records processed event exactly once")]
    public async Task Passenger_records_processed_event_exactly_once()
    {
        var store = new FakePassengerStore();
        var processor = new PassengerEventProcessor(store, NullLogger<PassengerEventProcessor>.Instance);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var json = EnvelopeJson(eventId, "st-1", "ts-1", new { profileReference = "pref-1" });

        var marked = await processor.ProcessAsync(json);

        Assert.True(marked);
        Assert.Single(store.Passengers);
        var passenger = store.Passengers[0];
        Assert.Equal(eventId, passenger.Id);
        Assert.Equal("pref-1", passenger.ProfileReference);
    }

    [Fact(DisplayName = "Duplicate passenger delivery does not create duplicate profile")]
    public async Task Duplicate_passenger_delivery_does_not_create_duplicate_profile()
    {
        var store = new FakePassengerStore();
        var processor = new PassengerEventProcessor(store, NullLogger<PassengerEventProcessor>.Instance);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var json = EnvelopeJson(eventId, "st-1", "ts-1", new { profileReference = "pref-1" });

        var first = await processor.ProcessAsync(json);
        var second = await processor.ProcessAsync(json);

        Assert.True(first);
        Assert.False(second);
        Assert.Single(store.Passengers);
    }

    [Fact(DisplayName = "Malformed envelope throws invalid operation")]
    public async Task Malformed_envelope_throws_invalid_operation()
    {
        var store = new FakePassengerStore();
        var processor = new PassengerEventProcessor(store, NullLogger<PassengerEventProcessor>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync("{ not json"));
        Assert.Contains("Malformed passenger envelope", ex.Message);
    }

    [Fact(DisplayName = "Envelope missing event id throws invalid operation")]
    public async Task Envelope_missing_event_id_throws_invalid_operation()
    {
        var store = new FakePassengerStore();
        var processor = new PassengerEventProcessor(store, NullLogger<PassengerEventProcessor>.Instance);
        var json = JsonSerializer.Serialize(new { specversion = "1.0", type = "x" }, JsonOptions);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(json));
        Assert.Contains("missing an event id", ex.Message);
    }

    private static string EnvelopeJson(string eventId, string stationId, string equipmentId, object payload)
    {
        var envelope = new TelemetryEventEnvelope(
            "1.0",
            eventId,
            "urn:metromesh:passenger:loyalty.v1",
            "urn:metromesh:device:kiosk-1",
            "passenger/kiosk-1",
            DateTimeOffset.UtcNow,
            "application/json",
            "corr-pass-1",
            new TelemetryEventData(
                equipmentId,
                stationId,
                "passenger.profile.created",
                DateTimeOffset.UtcNow,
                JsonSerializer.SerializeToElement(payload)));

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    [Fact(DisplayName = "Store records loyalty program for passenger")]
    public async Task Store_records_loyalty_program_for_passenger()
    {
        var store = new FakePassengerStore();
        var program = new LoyaltyProgram("lp-1", "pass-1", "gold", "active", "terms-v1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

        await store.RecordLoyaltyProgramAsync(program);

        var programs = await store.GetLoyaltyProgramsAsync("pass-1");
        Assert.Single(programs);
        Assert.Equal("gold", programs[0].Tier);
        Assert.Equal("active", programs[0].Status);
    }

    [Fact(DisplayName = "Store retires loyalty program")]
    public async Task Store_retires_loyalty_program()
    {
        var store = new FakePassengerStore();
        var program = new LoyaltyProgram("lp-1", "pass-1", "gold", "active", "terms-v1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        await store.RecordLoyaltyProgramAsync(program);

        await store.RetireLoyaltyProgramAsync("lp-1");

        var retired = await store.GetLoyaltyProgramAsync("lp-1");
        Assert.NotNull(retired);
        Assert.Equal("retired", retired!.Status);
        Assert.NotNull(retired.RetiredAt);
    }

    [Fact(DisplayName = "Store records notification and retrieves by passenger")]
    public async Task Store_records_notification_and_retrieves_by_passenger()
    {
        var store = new FakePassengerStore();
        var notification = new Notification("n-1", "pass-1", "profile.updated", "passenger@email.com", "pending", DateTimeOffset.UtcNow);

        await store.RecordNotificationAsync(notification);

        var notifications = await store.GetNotificationsAsync("pass-1");
        Assert.Single(notifications);
        Assert.Equal("profile.updated", notifications[0].Type);
        Assert.Equal("passenger@email.com", notifications[0].Recipient);
    }

    [Fact(DisplayName = "Store records audit entry")]
    public async Task Store_records_audit_entry()
    {
        var store = new FakePassengerStore();
        var entry = new AuditEntry("a-1", "pass-1", "loyalty.created", "operator-1", "Created gold loyalty program", DateTimeOffset.UtcNow);

        await store.RecordAuditAsync(entry);

        var audit = await store.GetAuditAsync("pass-1");
        Assert.Single(audit);
        Assert.Equal("loyalty.created", audit[0].Action);
        Assert.Equal("operator-1", audit[0].Actor);
    }
}
