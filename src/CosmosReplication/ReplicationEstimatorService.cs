namespace CosmosReplication;

using System.Threading;
using System.Threading.Tasks;

using CosmosReplication.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ReplicationEstimatorService(ILogger<ReplicationEstimatorService> logger, IEnumerable<IContainerReplicationEstimator> estimators) : IHostedService
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
