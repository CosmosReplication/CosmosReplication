using System.Collections.ObjectModel;
using CosmosReplication.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CosmosReplication;

public class ReplicationEstimatorService(ILogger<ReplicationEstimatorService> logger, ReadOnlyCollection<IContainerReplicationEstimator> estimators) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		logger.ServiceStarting(nameof(ReplicationEstimatorService));
		foreach (var estimator in estimators)
		{
			if (await estimator.InitializeAsync(cancellationToken))
			{
				await estimator.StartAsync();
			}
		}

		logger.ServiceStarted(nameof(ReplicationEstimatorService));
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		foreach (var estimator in estimators)
		{
			await estimator.StopAsync();
		}
	}
}
