using CosmosReplication;
using CosmosReplication.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddDefaultHealthChecks().ConfigureOpenTelemetry();
builder.Services.AddCosmosReplication("ReplicationConfiguration", ["startup", "ready"]);

await using var app = builder.Build();
app.MapDefaultEndpoints();

await app.RunAsync();
