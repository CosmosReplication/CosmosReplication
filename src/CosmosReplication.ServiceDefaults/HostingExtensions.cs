using System.Reflection;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace CosmosReplication.ServiceDefaults;

public static class HostingExtensions
{
    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Add a default liveness check to ensure app is responsive
        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.Services.Configure<LoggerFactoryOptions>(lfo => lfo.ActivityTrackingOptions =
                    ActivityTrackingOptions.SpanId
                    | ActivityTrackingOptions.TraceId
                    | ActivityTrackingOptions.ParentId
                    | ActivityTrackingOptions.TraceState
                    | ActivityTrackingOptions.TraceFlags
                    | ActivityTrackingOptions.Tags
                    | ActivityTrackingOptions.Baggage);

        builder.Services.Configure<OpenTelemetryLoggerOptions>((otelLoggerOptions) => otelLoggerOptions.IncludeScopes = true);

        var hostName =
                Environment.GetEnvironmentVariable("HOSTNAME")
                ?? Environment.GetEnvironmentVariable("COMPUTERNAME")
                ?? Environment.MachineName;

        // Create a dictionary of resource attributes.
        var resourceAttributes = new Dictionary<string, object>
        {
            { "service.name", builder.Environment.ApplicationName },
            { "service.namespace", "CosmosReplication" },
            { "service.instance.id", hostName },
            { "service.version", Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown" },
        };

        var appInsightsConnectionString = builder.Configuration.GetValue("APPLICATIONINSIGHTS_CONNECTION_STRING", string.Empty);
        appInsightsConnectionString = builder.Configuration.GetValue("AzureMonitor:ConnectionString", appInsightsConnectionString);

        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            builder.Services.AddOpenTelemetry().UseAzureMonitor(o =>
            {
                o.ConnectionString = appInsightsConnectionString;
                o.EnableLiveMetrics = true;
                o.SamplingRatio = 1;
            })
            .WithMetrics(mb => mb.AddMeter("CosmosReplication"))
            .ConfigureResource(rb => rb.AddAttributes(resourceAttributes));
        }
        else if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter()
            .WithMetrics(mb => mb.AddMeter("CosmosReplication"))
            .ConfigureResource(rb => rb.AddAttributes(resourceAttributes));
        }

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks("/healthz/startup", new HealthCheckOptions
        {
            Predicate = reg => reg.Tags.Contains("startup"),
        });
        app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
        {
            Predicate = reg => reg.Tags.Contains("ready"),
        });
        app.MapHealthChecks("/healthz/live", new HealthCheckOptions
        {
            Predicate = reg => reg.Tags.Contains("live"),
        });

        return app;
    }
}
