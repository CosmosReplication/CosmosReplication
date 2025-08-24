using System.Collections.ObjectModel;
using CosmosReplication.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CosmosReplication;

public class ReplicationEstimatorHealthCheck(ReadOnlyCollection<IContainerReplicationEstimator> containerReplicationEstimators) : IHealthCheck
{
	public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		// Implement health check logic here
		var result = containerReplicationEstimators.All(cr => cr.Initialized && cr.Started)
			? HealthCheckResult.Healthy("All container replication estimators are initialized and started.")
			: HealthCheckResult.Unhealthy("One or more container replication estimators are not initialized or started.");

		return Task.FromResult(result);
	}
}
