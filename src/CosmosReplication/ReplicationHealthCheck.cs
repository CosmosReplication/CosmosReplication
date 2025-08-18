namespace CosmosReplication;

using Microsoft.Extensions.Diagnostics.HealthChecks;

public class ReplicationHealthCheck(IEnumerable<ContainerReplicationProcessor> containerReplications) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Implement health check logic here
        var result = containerReplications.All(cr => cr.Initialized && cr.Started)
            ? HealthCheckResult.Healthy("All container replications are initialized and started.")
            : HealthCheckResult.Unhealthy("One or more container replications are not initialized or started.");

        return Task.FromResult(result);
    }
}
