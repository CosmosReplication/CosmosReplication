using System.Collections.ObjectModel;
using CosmosReplication.Interfaces;

using Microsoft.Extensions.Logging;

namespace CosmosReplication;

public class ReplicationEstimatorService : IReplicationEstimatorService
{
    private readonly ILogger<ReplicationEstimatorService> _logger;
    private readonly ReadOnlyCollection<IContainerReplicationEstimator> _estimators;

    public ReplicationEstimatorService(ILogger<ReplicationEstimatorService> logger, ReadOnlyCollection<IContainerReplicationEstimator> estimators)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _estimators = estimators ?? throw new ArgumentNullException(nameof(estimators));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.ServiceStarting(nameof(ReplicationEstimatorService));
        foreach (var estimator in _estimators)
        {
            if (await estimator.InitializeAsync(cancellationToken))
            {
                await estimator.StartAsync();
            }
        }

        _logger.ServiceStarted(nameof(ReplicationEstimatorService));
    }

    public async Task StopAsync()
    {
        foreach (var estimator in _estimators)
        {
            await estimator.StopAsync();
        }
    }
}
