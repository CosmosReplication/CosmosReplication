using System.Collections.ObjectModel;
using CosmosReplication.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CosmosReplication;

public class ReplicationService(ILogger<ReplicationService> logger, ReadOnlyCollection<IContainerReplicationProcessor> containerReplications) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			logger.ServiceStarting(nameof(ReplicationService));
			foreach (var containerReplication in containerReplications)
			{
				if (await containerReplication.InitializeAsync(cancellationToken))
				{
					await containerReplication.StartAsync();
				}
			}

			logger.ServiceStarted(nameof(ReplicationService));
		}
		catch (Exception ex)
		{
			logger.ServiceStartFailed(nameof(ReplicationService), ex);
		}
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		logger.ServiceStopping(nameof(ReplicationService));
		foreach (var containerReplication in containerReplications)
		{
			await containerReplication.StopAsync();
		}

		logger.ServiceStopped(nameof(ReplicationService));
	}
}
