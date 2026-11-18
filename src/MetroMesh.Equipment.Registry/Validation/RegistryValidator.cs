using MetroMesh.Equipment.Registry.Contracts;

namespace MetroMesh.Equipment.Registry.Validation;

public static class RegistryValidator
{
    private static readonly HashSet<string> StationStatuses = new(StringComparer.Ordinal) { "open", "closed", "offline" };
    private static readonly HashSet<string> EquipmentKinds = new(StringComparer.Ordinal) { "turnstile", "camera" };
    private static readonly HashSet<string> EquipmentStatuses = new(StringComparer.Ordinal) { "active", "inactive" };

    public static IDictionary<string, string[]> ValidateStation(StationRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Station name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            errors["address"] = ["Station address is required."];
        }

        if (request.Status is not null && !StationStatuses.Contains(request.Status))
        {
            errors["status"] = ["Status must be one of: open, closed, offline."];
        }

        return errors;
    }

    public static IDictionary<string, string[]> ValidateStationStatusUpdate(string? status)
    {
        var errors = new Dictionary<string, string[]>();
        if (status is null || !StationStatuses.Contains(status))
        {
            errors["status"] = ["Status must be one of: open, closed, offline."];
        }

        return errors;
    }

    public static IDictionary<string, string[]> ValidateEquipment(EquipmentRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["Request body is required."];
            return errors;
        }

        if (request.Kind is null || !EquipmentKinds.Contains(request.Kind))
        {
            errors["kind"] = ["Kind must be one of: turnstile, camera."];
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            errors["model"] = ["Equipment model is required."];
        }

        if (string.IsNullOrWhiteSpace(request.StationId))
        {
            errors["stationId"] = ["Station id is required."];
        }

        if (request.Status is not null && !EquipmentStatuses.Contains(request.Status))
        {
            errors["status"] = ["Status must be one of: active, inactive."];
        }

        if (request.MaintenanceThreshold is <= 0)
        {
            errors["maintenanceThreshold"] = ["Maintenance threshold must be a positive integer."];
        }

        return errors;
    }
}
