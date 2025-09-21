using CosmosReplication.Interfaces;
using CosmosReplication.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CosmosReplication;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddCosmosReplication(
  this IServiceCollection services,
  string configSectionPath,
  IEnumerable<string>? healthCheckTags = null)
  {
    ArgumentNullException.ThrowIfNull(configSectionPath);
    services.AddOptions<ReplicationConfiguration>()
        .BindConfiguration(configSectionPath)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services.AddSingleton<ICosmosClientFactory, CosmosClientFactory>();
    services.AddSingleton<IReplicationMetrics, ReplicationMetrics>();
    services.AddSingleton(provider =>
    {
      var logger = provider.GetRequiredService<ILogger<ContainerReplicationProcessor>>();
      var cosmosClientFactory = provider.GetRequiredService<ICosmosClientFactory>();
      var options = provider.GetRequiredService<IOptions<ReplicationConfiguration>>();
      var replicationConfig = options.Value;

      return replicationConfig.ContainerReplications
              .Select(config =>
                  new ContainerReplicationProcessor(
                      logger,
                      cosmosClientFactory,
                      replicationConfig.Name,
                      config) as IContainerReplicationProcessor).ToList().AsReadOnly();
    });

    IEnumerable<string> tags = ["cosmosreplication"];
    if (healthCheckTags != null)
    {
      tags = tags.Union(healthCheckTags).Distinct();
    }

    services.AddHealthChecks().AddCheck<ReplicationHealthCheck>("Replication Health Check", tags: tags);
    services.AddHostedService<ReplicationService>();
    return services;
  }

  public static IServiceCollection AddCosmosReplicationEstimator(
      this IServiceCollection services,
      string configSectionPath,
      IEnumerable<string>? healthCheckTags = null)
  {
    ArgumentNullException.ThrowIfNull(configSectionPath);
    services.AddOptions<ReplicationConfiguration>()
        .BindConfiguration(configSectionPath)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services.AddSingleton<ICosmosClientFactory, CosmosClientFactory>();
    services.AddSingleton<IReplicationMetrics, ReplicationMetrics>();
    services.AddSingleton(provider =>
    {
      var logger = provider.GetRequiredService<ILogger<ContainerReplicationEstimator>>();
      var replicationMetrics = provider.GetRequiredService<IReplicationMetrics>();
      var cosmosClientFactory = provider.GetRequiredService<ICosmosClientFactory>();
      var options = provider.GetRequiredService<IOptions<ReplicationConfiguration>>();
      var replicationConfig = options.Value;

      return replicationConfig.ContainerReplications
              .Select(config =>
                  new ContainerReplicationEstimator(
                      logger,
                      replicationMetrics,
                      cosmosClientFactory,
                      replicationConfig.Name,
                      config) as IContainerReplicationEstimator).ToList().AsReadOnly();
    });

    IEnumerable<string> tags = ["cosmosreplication"];
    if (healthCheckTags != null)
    {
      tags = tags.Union(healthCheckTags).Distinct();
    }

    services.AddHealthChecks().AddCheck<ReplicationEstimatorHealthCheck>("Replication Estimator Health Check", tags: tags);
    services.AddSingleton<IReplicationEstimatorService, ReplicationEstimatorService>();
    return services;
  }
}
