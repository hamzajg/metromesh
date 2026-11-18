using MetroMesh.Telemetry.Core.Contracts;

namespace MetroMesh.Telemetry.Ingest.Validation;

public static class TelemetryEventValidator
{
    public const string TypePrefix = "equipment.telemetry.";

    public static Dictionary<string, string[]> Validate(TelemetryEventRequest? request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (request is null)
        {
            Add(errors, "body", "request body is required");
            return errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        }

        Require(errors, "specversion", request.SpecVersion, "specversion is required");
        if (!string.IsNullOrWhiteSpace(request.SpecVersion) && request.SpecVersion != "1.0")
        {
            Add(errors, "specversion", "specversion must be 1.0");
        }

        Require(errors, "id", request.Id, "id is required");
        Require(errors, "type", request.Type, "type is required");
        if (!string.IsNullOrWhiteSpace(request.Type) && !request.Type.StartsWith(TypePrefix, StringComparison.Ordinal))
        {
            Add(errors, "type", $"type must start with {TypePrefix}");
        }

        Require(errors, "source", request.Source, "source is required");
        Require(errors, "subject", request.Subject, "subject is required");
        Require(errors, "time", request.Time.HasValue ? "present" : null, "time is required");
        Require(errors, "datacontenttype", request.DataContentType, "datacontenttype is required");

        if (request.Data is null)
        {
            Add(errors, "data", "data is required");
        }
        else
        {
            Require(errors, "data.equipmentId", request.Data.EquipmentId, "data.equipmentId is required");
            Require(errors, "data.stationId", request.Data.StationId, "data.stationId is required");
            Require(errors, "data.eventType", request.Data.EventType, "data.eventType is required");
            Require(errors, "data.occurredAt", request.Data.OccurredAt.HasValue ? "present" : null, "data.occurredAt is required");
        }

        return errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private static void Require(IDictionary<string, List<string>> errors, string field, string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(errors, field, message);
        }
    }

    private static void Add(IDictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list))
        {
            list = [];
            errors[field] = list;
        }

        list.Add(message);
    }
}
