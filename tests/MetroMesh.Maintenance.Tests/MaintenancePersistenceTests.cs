using MetroMesh.Maintenance.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace MetroMesh.Maintenance.Tests;

public sealed class MaintenancePersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private PostgresMaintenanceStore _store = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var dataSource = NpgsqlDataSource.Create(_postgres.GetConnectionString());
        _store = new PostgresMaintenanceStore(dataSource);
        await _store.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Fact(DisplayName = "Maintenance request and work order persist and read back")]
    public async Task Maintenance_request_and_work_order_persist_and_read_back()
    {
        var request = await _store.InsertRequestAsync(
            "eq_turnstile_1",
            "Turnstile gate arm stuck",
            "HIGH",
            "manual",
            "Left gate, platform 2",
            CancellationToken.None);

        Assert.StartsWith("mr_", request.Id);
        Assert.Equal("high", request.Severity);
        Assert.Equal("manual", request.Source);
        Assert.Equal("open", request.Status);

        var fetchedRequest = await _store.GetRequestAsync(request.Id);
        Assert.NotNull(fetchedRequest);
        Assert.Equal("eq_turnstile_1", fetchedRequest!.EquipmentId);

        var order = await _store.InsertWorkOrderAsync(
            request.Id,
            "eq_turnstile_1",
            "REPAIR",
            "URGENT",
            "Replace gate arm",
            "Part on order",
            CancellationToken.None);

        Assert.StartsWith("wo_", order.Id);
        Assert.Equal(request.Id, order.RequestId);
        Assert.Equal("repair", order.Type);
        Assert.Equal("urgent", order.Priority);
        Assert.Equal("open", order.Status);

        var fetchedOrder = await _store.GetWorkOrderAsync(order.Id);
        Assert.NotNull(fetchedOrder);
        Assert.Equal("Replace gate arm", fetchedOrder!.Title);

        Assert.Null(await _store.GetWorkOrderAsync("wo_missing"));
        Assert.True(await _store.PingAsync());
    }

    [Fact(DisplayName = "Work order transitions persist history atomically")]
    public async Task Work_order_transitions_persist_history_atomically()
    {
        var order = await _store.InsertWorkOrderAsync(
            null,
            "eq_turnstile_1",
            "inspection",
            "low",
            "Safety check",
            null,
            CancellationToken.None);

        var assigned = await _store.TransitionWorkOrderAsync(order.Id, "assigned", "tech-7", null, null, CancellationToken.None);
        Assert.NotNull(assigned);
        Assert.Equal("assigned", assigned!.Status);
        Assert.Equal("tech-7", assigned.Assignee);

        var completed = await _store.TransitionWorkOrderAsync(order.Id, "completed", null, "All clear", null, CancellationToken.None);
        Assert.Equal("completed", completed!.Status);
        Assert.Equal("All clear", completed.Resolution);

        var history = await _store.ListWorkOrderHistoryAsync(order.Id);
        Assert.Equal(2, history.Count);
        Assert.Equal("open", history[0].FromStatus);
        Assert.Equal("assigned", history[0].ToStatus);
        Assert.Equal("assigned", history[1].FromStatus);
        Assert.Equal("completed", history[1].ToStatus);

        var open = await _store.ListWorkOrdersAsync("open", null);
        Assert.DoesNotContain(open, w => w.Id == order.Id);

        var byEquipment = await _store.ListWorkOrdersAsync(null, "eq_turnstile_1");
        Assert.Contains(byEquipment, w => w.Id == order.Id);
    }
}
