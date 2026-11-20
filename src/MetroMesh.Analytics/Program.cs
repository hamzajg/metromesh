using MetroMesh.Analytics.Persistence;
using MetroMesh.Analytics.Services;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AnalyticsDb")
    ?? builder.Configuration["METROMESH_DB"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:AnalyticsDb or METROMESH_DB is required.");
}

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<IAnalyticsStore, AnalyticsStore>();
builder.Services.AddSingleton<AnalyticsEventProcessor>();
builder.Services.AddHostedService<AnalyticsConsumerService>();
builder.Services.AddHostedService<AnalyticsHealthService>();

var host = builder.Build();

await host.Services.GetRequiredService<IAnalyticsStore>().InitializeAsync();
await host.RunAsync();
