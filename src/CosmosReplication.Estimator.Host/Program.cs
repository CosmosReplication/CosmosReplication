using CosmosReplication;
using CosmosReplication.Estimator.Host;
using CosmosReplication.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddDefaultHealthChecks().ConfigureOpenTelemetry();
builder.Services.AddHttpClient();
builder.Services.AddCosmosReplicationEstimator("ReplicationConfiguration");
builder.Services.AddHostedService<Worker>();

await using var app = builder.Build();
app.MapDefaultEndpoints();
await app.RunAsync();
