namespace CosmosReplication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ReplicationService(ILogger<ReplicationService> logger, IEnumerable<ContainerReplication> containerReplications) : IHostedService
{
    private readonly ILogger<ReplicationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IEnumerable<ContainerReplication> _containerReplications = containerReplications ?? throw new ArgumentNullException(nameof(containerReplications));

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.ServiceStarting(nameof(ReplicationService));
            foreach (var containerReplication in _containerReplications)
            {
                if (await containerReplication.InititializeAsync(cancellationToken))
                {
                    await containerReplication.StartAsync();
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
        foreach (var containerReplication in _containerReplications)
        {
            await containerReplication.StopAsync();
        }
        _logger.ServiceStopped(nameof(ReplicationService));
    }
}
