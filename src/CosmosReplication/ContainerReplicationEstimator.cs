using System.Collections.ObjectModel;

using CosmosReplication.Interfaces;
using CosmosReplication.Models;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace CosmosReplication;

public class ContainerReplicationEstimator : IContainerReplicationEstimator
{
    private readonly ReadOnlyCollection<KeyValuePair<string, object?>> _configDictionary;
    private readonly ILogger<ContainerReplicationEstimator> _logger;
    private readonly IReplicationMetrics _metrics;
    private readonly ICosmosClientFactory _cosmosClientFactory;
    private readonly string _replicationName;
    private readonly ContainerReplicationConfiguration _config;
    private ChangeFeedProcessor _changeFeedEstimator = null!;

    public ContainerReplicationEstimator(
        ILogger<ContainerReplicationEstimator> logger,
        IReplicationMetrics metrics,
        ICosmosClientFactory cosmosClientFactory,
        string replicationName,
        ContainerReplicationConfiguration config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _cosmosClientFactory = cosmosClientFactory ?? throw new ArgumentNullException(nameof(cosmosClientFactory));
        _replicationName = replicationName ?? throw new ArgumentNullException(nameof(replicationName));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _configDictionary = new List<KeyValuePair<string, object?>>
    {
        new(nameof(config.SourceAccount), config.SourceAccount),
        new(nameof(config.SourceDatabase), config.SourceDatabase),
        new(nameof(config.SourceContainer), config.SourceContainer),
        new(nameof(config.DestinationAccount), config.DestinationAccount),
        new(nameof(config.DestinationDatabase), config.DestinationDatabase),
        new(nameof(config.DestinationContainer), config.DestinationContainer),
        new(nameof(config.LeaseAccount), config.LeaseAccount),
        new(nameof(config.LeaseDatabase), config.LeaseDatabase),
        new(nameof(config.LeaseContainer), config.LeaseContainer),
    }.AsReadOnly();
    }

    public bool Initialized { get; private set; }

    public bool Started { get; private set; }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(_configDictionary))
        {
            if (!Initialized)
            {
                try
                {
                    _logger.ServiceInitializing(nameof(ContainerReplicationEstimator));

                    var sourceContainer = _cosmosClientFactory.GetCosmosClient(_config.SourceAccount)
                        .GetContainer(_config.SourceDatabase, _config.SourceContainer);
                    var leaseContainer = _cosmosClientFactory.GetCosmosClient(_config.LeaseAccount)
                        .GetContainer(_config.LeaseDatabase, _config.LeaseContainer);

                    // Checks if all required containers exist.
                    await sourceContainer.ReadContainerAsync(cancellationToken: cancellationToken);
                    await leaseContainer.ReadContainerAsync(cancellationToken: cancellationToken);

                    var changeFeedEstimator = sourceContainer
                        .GetChangeFeedEstimatorBuilder(_replicationName, ReportEstimatedPendingChangesAsync, TimeSpan.FromSeconds(60))
                        .WithLeaseContainer(leaseContainer)
                        .Build();

                    Initialized = true;
                    _changeFeedEstimator = changeFeedEstimator;

                    _logger.ServiceInitialized(nameof(ContainerReplicationEstimator));
                }
                catch (Exception ex)
                {
                    _logger.ServiceInitializationFailed(nameof(ContainerReplicationEstimator), ex);
                }
            }
            else
            {
                _logger.ServiceAlreadyInitialized(nameof(ContainerReplicationEstimator));
            }

            return Initialized;
        }
    }

    public async Task StartAsync()
    {
        using (_logger.BeginScope(_configDictionary))
        {
            if (Initialized && !Started)
            {
                try
                {
                    _logger.ServiceStarting(nameof(ContainerReplicationEstimator));
                    await _changeFeedEstimator.StartAsync();
                    Started = true;
                    _logger.ServiceStarted(nameof(ContainerReplicationEstimator));
                }
                catch (Exception ex)
                {
                    _logger.ServiceStartFailed(nameof(ContainerReplicationEstimator), ex);
                }
            }
            else if (!Initialized)
            {
                _logger.ServiceNotInitialized(nameof(ContainerReplicationEstimator));
            }
            else
            {
                _logger.ServiceAlreadyStarted(nameof(ContainerReplicationEstimator));
            }
        }
    }

    public async Task StopAsync()
    {
        using (_logger.BeginScope(_configDictionary))
        {
            if (Initialized && Started)
            {
                _logger.ServiceStopping(nameof(ContainerReplicationEstimator));
                await _changeFeedEstimator.StopAsync();
                _logger.ServiceStopped(nameof(ContainerReplicationEstimator));
            }
            else if (!Initialized)
            {
                _logger.ServiceNotInitialized(nameof(ContainerReplicationEstimator));
            }
            else
            {
                _logger.ServiceNotStarted(nameof(ContainerReplicationEstimator));
            }
        }
    }

    private Task ReportEstimatedPendingChangesAsync(long estimatedPendingChanges, CancellationToken cancellationToken)
    {
        _metrics.RecordEstimatedPendingChanges(estimatedPendingChanges, [.. _configDictionary]);
        return Task.CompletedTask;
    }
}
