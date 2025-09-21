using System.Collections.ObjectModel;
using CosmosReplication.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CosmosReplication;

public class ReplicationHealthCheck : IHealthCheck
{
  private readonly ReadOnlyCollection<IContainerReplicationProcessor> _replications;

  public ReplicationHealthCheck(ReadOnlyCollection<IContainerReplicationProcessor> replications) => _replications = replications ?? throw new ArgumentNullException(nameof(replications));

  public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
  {
    // Implement health check logic here
    var result = _replications.All(cr => cr.Initialized && cr.Started)
        ? HealthCheckResult.Healthy("All container replications are initialized and started.")
        : HealthCheckResult.Unhealthy("One or more container replications are not initialized or started.");

    return Task.FromResult(result);
  }
}
