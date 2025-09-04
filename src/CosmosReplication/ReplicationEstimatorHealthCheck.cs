using System.Collections.ObjectModel;
using CosmosReplication.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CosmosReplication;

public class ReplicationEstimatorHealthCheck : IHealthCheck
{
    private readonly ReadOnlyCollection<IContainerReplicationEstimator> _estimators;

    public ReplicationEstimatorHealthCheck(ReadOnlyCollection<IContainerReplicationEstimator> estimators) => _estimators = estimators ?? throw new ArgumentNullException(nameof(estimators));

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Implement health check logic here
        var result = _estimators.All(cr => cr.Initialized && cr.Started)
            ? HealthCheckResult.Healthy("All container replication estimators are initialized and started.")
            : HealthCheckResult.Unhealthy("One or more container replication estimators are not initialized or started.");

        return Task.FromResult(result);
    }
}
