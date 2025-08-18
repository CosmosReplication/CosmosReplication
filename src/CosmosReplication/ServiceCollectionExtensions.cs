using CosmosReplication.Interfaces;
using CosmosReplication.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CosmosReplication;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddCosmosReplication(
		this IServiceCollection services,
		ReplicationConfiguration replicationConfiguration)
	{
		ArgumentNullException.ThrowIfNull(replicationConfiguration);
		services.AddSingleton(replicationConfiguration);
		services.AddSingleton<ICosmosClientFactory, CosmosClientFactory>();
		foreach (var config in replicationConfiguration.ContainerReplications)
		{
			services.AddSingleton<IContainerReplicationProcessor, ContainerReplicationProcessor>(provider =>
			{
				var factory = provider.GetRequiredService<ICosmosClientFactory>();
				var logger = provider.GetRequiredService<ILogger<ContainerReplicationProcessor>>();
				return new ContainerReplicationProcessor(logger, factory, replicationConfiguration.Name, config);
			});
		}

		services.AddHealthChecks().AddCheck<ReplicationHealthCheck>("Replication Health Check", tags: ["cosmosreplication"]);
		services.AddHostedService<ReplicationService>();
		return services;
	}

	public static IServiceCollection AddCosmosReplicationEstimator(
		this IServiceCollection services,
		ReplicationConfiguration replicationConfiguration)
	{
		ArgumentNullException.ThrowIfNull(replicationConfiguration);
		services.AddSingleton(replicationConfiguration);
		services.AddSingleton<ICosmosClientFactory, CosmosClientFactory>();
		services.AddSingleton<IReplicationMetrics, ReplicationMetrics>();
		foreach (var config in replicationConfiguration.ContainerReplications)
		{
			services.AddSingleton<IContainerReplicationEstimator, ContainerReplicationEstimator>(provider =>
			{
				var logger = provider.GetRequiredService<ILogger<ContainerReplicationEstimator>>();
				var factory = provider.GetRequiredService<ICosmosClientFactory>();
				var metrics = provider.GetRequiredService<IReplicationMetrics>();
				return new ContainerReplicationEstimator(logger, metrics, factory, replicationConfiguration.Name, config);
			});
		}

		services.AddHostedService<ReplicationEstimatorService>();
		return services;
	}
}
