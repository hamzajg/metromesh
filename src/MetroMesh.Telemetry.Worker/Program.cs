using MetroMesh.Telemetry.Core.Persistence;
using MetroMesh.Telemetry.Worker.Services;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TelemetryDb")
    ?? builder.Configuration["METROMESH_DB"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:TelemetryDb or METROMESH_DB is required.");
}

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<ITelemetryEventStore, PostgresTelemetryEventStore>();
builder.Services.AddSingleton<TelemetryEventProcessor>();
builder.Services.AddHostedService<TelemetryConsumerService>();

var host = builder.Build();

await host.Services.GetRequiredService<ITelemetryEventStore>().InitializeAsync();
await host.RunAsync();
