using Azure.Monitor.OpenTelemetry.AspNetCore;

using CosmosReplication;
using CosmosReplication.Models;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddOpenTelemetry(logging =>
		{
			logging.IncludeFormattedMessage = true;
			logging.IncludeScopes = true;
		});
builder.Services.AddOpenTelemetry().WithMetrics(mb => mb.AddMeter("CosmosReplication"));
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"])
|| !string.IsNullOrWhiteSpace(builder.Configuration["AzureMonitor:ConnectionString"]))
{
	builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

builder.Services.AddHealthChecks();

var replicationConfiguration = builder.Configuration.GetRequiredSection("ReplicationConfiguration").Get<ReplicationConfiguration>();
builder.Services.AddCosmosReplicationEstimator(replicationConfiguration);

var app = builder.Build();
app.MapHealthChecks("/healthz/startup");
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
	Predicate = _ => false,
});
app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
	Predicate = _ => false,
});

app.Run();
