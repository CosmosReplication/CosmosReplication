using System.Collections.ObjectModel;

using CosmosReplication.Interfaces;
using CosmosReplication.Models;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace CosmosReplication;

public class ContainerReplicationEstimator(
	ILogger<ContainerReplicationEstimator> logger,
	IReplicationMetrics metrics,
	ICosmosClientFactory cosmosClientFactory,
	string replicationName,
	ContainerReplicationConfiguration config) : IContainerReplicationEstimator
{
	private readonly ReadOnlyCollection<KeyValuePair<string, object?>> _configDictionary = new List<KeyValuePair<string, object?>>
	{
		new(nameof(config.SourceAccount), config.SourceAccount),
		new(nameof(config.SourceDatabase), config.SourceDatabase),
		new(nameof(config.SourceContainer), config.SourceContainer),
		new(nameof(config.DestinationAccount), config.DestinationAccount),
		new(nameof(config.DestinationDatabase), config.DestinationDatabase),
		new(nameof(config.DestinationContainer), config.DestinationContainer),
		new(nameof(config.LeaseAccount), config.LeaseAccount),
		new(nameof(config.LeaseDatabase), config.LeaseDatabase),
		new(nameof(config.LeaseContainer), config.LeaseContainer),
	}.AsReadOnly();

	private ChangeFeedProcessor _changeFeedEstimator = null!;

	public bool Initialized { get; private set; }

	public bool Started { get; private set; }

	public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
	{
		using (logger.BeginScope(_configDictionary))
		{
			if (!Initialized)
			{
				try
				{
					logger.ServiceInitializing(nameof(ContainerReplicationEstimator));

					var sourceContainer = cosmosClientFactory.GetCosmosClient(config.SourceAccount)
						.GetContainer(config.SourceDatabase, config.SourceContainer);
					var leaseContainer = cosmosClientFactory.GetCosmosClient(config.LeaseAccount)
						.GetContainer(config.LeaseDatabase, config.LeaseContainer);

					// Checks if all required containers exist.
					await sourceContainer.ReadContainerAsync(cancellationToken: cancellationToken);
					await leaseContainer.ReadContainerAsync(cancellationToken: cancellationToken);

					var changeFeedEstimator = sourceContainer
						.GetChangeFeedEstimatorBuilder(replicationName, ReportEstimatedPendingChangesAsync, TimeSpan.FromSeconds(60))
						.WithLeaseContainer(leaseContainer)
						.Build();

					Initialized = true;
					_changeFeedEstimator = changeFeedEstimator;

					logger.ServiceInitialized(nameof(ContainerReplicationEstimator));
				}
				catch (Exception ex)
				{
					logger.ServiceInitializationFailed(nameof(ContainerReplicationEstimator), ex);
				}
			}
			else
			{
				logger.ServiceAlreadyInitialized(nameof(ContainerReplicationEstimator));
			}

			return Initialized;
		}
	}

	public async Task StartAsync()
	{
		using (logger.BeginScope(_configDictionary))
		{
			if (Initialized && !Started)
			{
				try
				{
					logger.ServiceStarting(nameof(ContainerReplicationEstimator));
					await _changeFeedEstimator.StartAsync();
					Started = true;
					logger.ServiceStarted(nameof(ContainerReplicationEstimator));
				}
				catch (Exception ex)
				{
					logger.ServiceStartFailed(nameof(ContainerReplicationEstimator), ex);
				}
			}
			else if (!Initialized)
			{
				logger.ServiceNotInitialized(nameof(ContainerReplicationEstimator));
			}
			else
			{
				logger.ServiceAlreadyStarted(nameof(ContainerReplicationEstimator));
			}
		}
	}

	public async Task StopAsync()
	{
		using (logger.BeginScope(_configDictionary))
		{
			if (Initialized && Started)
			{
				logger.ServiceStopping(nameof(ContainerReplicationEstimator));
				await _changeFeedEstimator.StopAsync();
				logger.ServiceStopped(nameof(ContainerReplicationEstimator));
			}
			else if (!Initialized)
			{
				logger.ServiceNotInitialized(nameof(ContainerReplicationEstimator));
			}
			else
			{
				logger.ServiceNotStarted(nameof(ContainerReplicationEstimator));
			}
		}
	}

	private Task ReportEstimatedPendingChangesAsync(long estimatedPendingChanges, CancellationToken cancellationToken)
	{
		metrics.RecordEstimatedPendingChanges(estimatedPendingChanges, [.. _configDictionary]);
		return Task.CompletedTask;
	}
}
