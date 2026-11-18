using MetroMesh.Maintenance.Contracts;

namespace MetroMesh.Maintenance.Validation;

public static class MaintenanceValidator
{
    private static readonly HashSet<string> Severities = new(StringComparer.OrdinalIgnoreCase)
    {
        "low", "medium", "high", "critical"
    };

    private static readonly HashSet<string> Sources = new(StringComparer.OrdinalIgnoreCase)
    {
        "manual", "signal"
    };

    private static readonly HashSet<string> WorkOrderTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "repair", "inspection", "upgrade", "other"
    };

    private static readonly HashSet<string> Priorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "low", "medium", "high", "urgent"
    };

    public static IReadOnlyList<string> Validate(CreateMaintenanceRequest? request)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("request body is required");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.EquipmentId))
        {
            errors.Add("equipmentId is required");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add("reason is required");
        }
        else if (request.Reason.Length > 500)
        {
            errors.Add("reason must be 500 characters or fewer");
        }

        if (string.IsNullOrWhiteSpace(request.Severity) || !Severities.Contains(request.Severity))
        {
            errors.Add("severity must be one of: low, medium, high, critical");
        }

        if (!string.IsNullOrWhiteSpace(request.Source) && !Sources.Contains(request.Source))
        {
            errors.Add("source must be one of: manual, signal");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(CreateWorkOrder? request)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("request body is required");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.EquipmentId))
        {
            errors.Add("equipmentId is required");
        }

        if (string.IsNullOrWhiteSpace(request.Type) || !WorkOrderTypes.Contains(request.Type))
        {
            errors.Add("type must be one of: repair, inspection, upgrade, other");
        }

        if (string.IsNullOrWhiteSpace(request.Priority) || !Priorities.Contains(request.Priority))
        {
            errors.Add("priority must be one of: low, medium, high, urgent");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors.Add("title is required");
        }
        else if (request.Title.Length > 200)
        {
            errors.Add("title must be 200 characters or fewer");
        }

        return errors;
    }
}
