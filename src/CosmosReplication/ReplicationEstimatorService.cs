using CosmosReplication.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CosmosReplication;

public class ReplicationEstimatorService(ILogger<ReplicationEstimatorService> logger, IEnumerable<IContainerReplicationEstimator> estimators) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		logger.ServiceStarting(nameof(ReplicationEstimatorService));
		foreach (var estimator in estimators)
		{
			if (await estimator.InitializeAsync(cancellationToken).ConfigureAwait(false))
			{
				await estimator.StartAsync().ConfigureAwait(false);
			}
		}

		logger.ServiceStarted(nameof(ReplicationEstimatorService));
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		foreach (var estimator in estimators)
		{
			await estimator.StopAsync().ConfigureAwait(false);
		}
	}
}
