using Azure.Monitor.OpenTelemetry.AspNetCore;

using CosmosReplication;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry;

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

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
	builder.Services.AddOpenTelemetry().UseOtlpExporter();
}

builder.Services.AddHealthChecks();
if (builder.Configuration.GetValue("ADD_REPLICATION_ESTIMATOR", false))
{
	builder.Services.AddCosmosReplicationEstimator("ReplicationConfiguration");
}
else
{
	builder.Services.AddCosmosReplication("ReplicationConfiguration");
}

using var app = builder.Build();
app.MapHealthChecks("/healthz/startup");
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
	Predicate = _ => false,
});
app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
	Predicate = _ => false,
});

await app.RunAsync();
