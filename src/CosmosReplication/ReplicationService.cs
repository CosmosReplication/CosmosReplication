using System.Collections.ObjectModel;
using CosmosReplication.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CosmosReplication;

public class ReplicationService : IHostedService
{
    private readonly ILogger<ReplicationService> _logger;
    private readonly ReadOnlyCollection<IContainerReplicationProcessor> _replications;

    public ReplicationService(ILogger<ReplicationService> logger, ReadOnlyCollection<IContainerReplicationProcessor> replications)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _replications = replications ?? throw new ArgumentNullException(nameof(replications));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.ServiceStarting(nameof(ReplicationService));
            foreach (var replication in _replications)
            {
                if (await replication.InitializeAsync(cancellationToken))
                {
                    await replication.StartAsync();
                }
            }

            _logger.ServiceStarted(nameof(ReplicationService));
        }
        catch (Exception ex)
        {
            _logger.ServiceStartFailed(nameof(ReplicationService), ex);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.ServiceStopping(nameof(ReplicationService));
        foreach (var replication in _replications)
        {
            await replication.StopAsync();
        }

        _logger.ServiceStopped(nameof(ReplicationService));
    }
}
