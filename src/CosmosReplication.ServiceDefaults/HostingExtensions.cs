using System.Reflection;
using System.Text.Json;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CosmosReplication.ServiceDefaults;

public static class HostingExtensions
{
  private static readonly JsonSerializerOptions HealthJsonOptions = new() { WriteIndented = true };

  public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    // Add a default liveness check to ensure app is responsive
    builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), ["live", "ready", "startup"]);
    return builder;
  }

  public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
  {
    ArgumentNullException.ThrowIfNull(builder);
    builder.Services.AddHttpContextAccessor();
    builder.Logging.Services.Configure<LoggerFactoryOptions>(lfo => lfo.ActivityTrackingOptions =
                ActivityTrackingOptions.SpanId
                | ActivityTrackingOptions.TraceId
                | ActivityTrackingOptions.ParentId
                | ActivityTrackingOptions.TraceState
                | ActivityTrackingOptions.TraceFlags
                | ActivityTrackingOptions.Tags
                | ActivityTrackingOptions.Baggage);

    builder.Services.Configure<OpenTelemetryLoggerOptions>(otelo =>
    {
      otelo.IncludeScopes = true;
      otelo.ParseStateValues = true;
      otelo.IncludeFormattedMessage = true;
    });

    builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
    {
      options.RecordException = true;
      options.Filter = RequestTracingFilter;
    });

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
    var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

    if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
    {
      builder.Services.AddOpenTelemetry().UseAzureMonitor()
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
      Predicate = reg => reg.Tags.Contains("startup")
    });
    app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
    {
      Predicate = reg => reg.Tags.Contains("ready")
    });
    app.MapHealthChecks("/healthz/live", new HealthCheckOptions
    {
      Predicate = reg => reg.Tags.Contains("live")
    });

    // Returns detailed JSON results for all health checks
    app.MapHealthChecks("/healthz/all", new HealthCheckOptions
    {
      Predicate = _ => true,
      ResponseWriter = JsonResponseWriterAsync
    });

    return app;
  }

  private static async Task JsonResponseWriterAsync(HttpContext context, HealthReport report)
  {
    context.Response.ContentType = "application/json";
    var result = new
    {
      status = report.Status.ToString(),
      totalDuration = Convert.ToInt32(report.TotalDuration.TotalMilliseconds),
      checks = report.Entries.Select(kvp => new
      {
        name = kvp.Key,
        status = kvp.Value.Status.ToString(),
        description = kvp.Value.Description,
        duration = Convert.ToInt32(kvp.Value.Duration.TotalMilliseconds),
        exception = kvp.Value.Exception?.Message,
        data = kvp.Value.Data.ToDictionary(d => d.Key, d => d.Value),
      }),
    };
    await context.Response.WriteAsync(JsonSerializer.Serialize(result, HealthJsonOptions));
  }

  private static bool RequestTracingFilter(HttpContext context) => !context.Request.Path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase);
}
