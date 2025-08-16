namespace CosmosReplication;


using System.Globalization;

using CosmosReplication.Inerfaces;
using CosmosReplication.Models;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

public class ContainerReplication
{

    public ContainerReplication(
        ILogger<ContainerReplication> logger,
        ICosmosClientFactory cosmosClientFactory,
        string replicationName,
        ContainerReplicationConfiguration config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cosmosClientFactory = cosmosClientFactory ?? throw new ArgumentNullException(nameof(cosmosClientFactory));
        _replicationName = replicationName ?? throw new ArgumentNullException(nameof(replicationName));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _configDictionary = new Dictionary<string, object?>
    {
        { nameof(config.SourceAccount), config.SourceAccount },
        { nameof(config.SourceDatabase), config.SourceDatabase },
        { nameof(config.SourceContainer), config.SourceContainer },
        { nameof(config.DestinationAccount), config.DestinationAccount },
        { nameof(config.DestinationDatabase), config.DestinationDatabase },
        { nameof(config.DestinationContainer), config.DestinationContainer },
        { nameof(config.LeaseAccount), config.LeaseAccount },
        { nameof(config.LeaseDatabase), config.LeaseDatabase },
        { nameof(config.LeaseContainer), config.LeaseContainer }
    };
    }

    private ChangeFeedProcessor _changeFeedProcessor = null!;
    private Container _sourceContainer = null!;
    private ContainerProperties _sourceContainerProperties = null!;
    public bool Initialized { get; private set; }
    private Container _destinationContainer = null!;
    private ContainerProperties _destinationContainerProperties = null!;
    public bool Started { get; private set; }
    private readonly ILogger<ContainerReplication> _logger;
    private readonly Dictionary<string, object?> _configDictionary;
    private readonly ICosmosClientFactory _cosmosClientFactory;
    private readonly string _replicationName;
    private readonly ContainerReplicationConfiguration _config;

    public async Task<bool> InititializeAsync(CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(_configDictionary))
        {
            if (!Initialized)
            {
                try
                {
                    _logger.ServiceInitializing(nameof(ContainerReplication));

                    var sourceContainer = _cosmosClientFactory.GetCosmosClient(_config.SourceAccount)
                        .GetContainer(_config.SourceDatabase, _config.SourceContainer);
                    var leaseContainer = _cosmosClientFactory.GetCosmosClient(_config.LeaseAccount)
                        .GetContainer(_config.LeaseDatabase, _config.LeaseContainer);
                    var destinationContainer = _cosmosClientFactory.GetCosmosClient(_config.DestinationAccount)
                        .GetContainer(_config.DestinationDatabase, _config.DestinationContainer);

                    // Checks if all required containers exist. 
                    var sourceContainerProperties = (await sourceContainer.ReadContainerAsync(cancellationToken: cancellationToken)).Resource;
                    await leaseContainer.ReadContainerAsync(cancellationToken: cancellationToken);
                    var destinationContainerProperties = (await destinationContainer.ReadContainerAsync(cancellationToken: cancellationToken)).Resource;

                    _changeFeedProcessor = sourceContainer
                        .GetChangeFeedProcessorBuilder<IDictionary<string, object?>>(_replicationName, MigrateChangesAsync)
                        .WithInstanceName(Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName)
                        .WithLeaseContainer(leaseContainer)
                        .WithMaxItems(_config.MaxItemCount ?? 100)
                        .WithPollInterval(_config.PollInterval ?? TimeSpan.FromSeconds(5))
                        .WithStartTime(_config.StartTime ?? DateTime.MinValue.ToUniversalTime())
                        .WithErrorNotification(OnError)
                        .Build();

                    _sourceContainer = sourceContainer;
                    _sourceContainerProperties = sourceContainerProperties;
                    _destinationContainer = destinationContainer;
                    _destinationContainerProperties = destinationContainerProperties;
                    Initialized = true;

                    _logger.ServiceInitialized(nameof(ContainerReplication));
                }
                catch (Exception ex)
                {
                    _logger.ServiceInitializationFailed(nameof(ContainerReplication), ex);
                }
            }
            else
            {
                _logger.ServiceAlreadyInitialized(nameof(ContainerReplication));
            }

            return Initialized;
        }
    }

    private Task OnError(string leaseToken, Exception exception)
    {
        using (_logger.BeginScope(_configDictionary))
        {
            _logger.LeaseProcessingError(exception, leaseToken);
            return Task.CompletedTask;
        }
    }

    public async Task StartAsync()
    {
        using (_logger.BeginScope(_configDictionary))
        {
            if (!Started)
            {
                try
                {
                    _logger.ServiceStarting(nameof(ContainerReplication));
                    await _changeFeedProcessor.StartAsync();
                    _logger.ServiceStarted(nameof(ContainerReplication));
                    Started = true;
                }
                catch (Exception ex)
                {
                    _logger.ServiceStartFailed(nameof(ContainerReplication), ex);
                    Started = false;
                }
            }
            else
            {
                _logger.ServiceAlreadyStarted(nameof(ContainerReplication));
            }
        }
    }

    public async Task StopAsync()
    {
        using (_logger.BeginScope(_configDictionary))
        {
            if (Initialized && Started)
            {
                _logger.ServiceStopping(nameof(ContainerReplication));
                await _changeFeedProcessor.StopAsync();
                _logger.ServiceStopped(nameof(ContainerReplication));
                Started = false;
            }
        }
    }

    public async Task MigrateChangesAsync(ChangeFeedProcessorContext context, IReadOnlyCollection<IDictionary<string, object?>> changes, CancellationToken cancellationToken)
    {
        if (_destinationContainer.Database.Client.ClientOptions.AllowBulkExecution
        && _config.BatchSize.HasValue
        && _config.BatchSize.Value > 1
        )
        {
            var chunks = changes.Chunk(_config.BatchSize.Value);
            foreach (var chunk in chunks)
            {
                var tasks = chunk.Select(item => UpsertDocumentAsync(item, cancellationToken));
                await Task.WhenAll(tasks);
            }
        }
        else
        {
            foreach (var item in changes)
            {
                await UpsertDocumentAsync(item, cancellationToken);
            }
        }
    }

    private async Task UpsertDocumentAsync(IDictionary<string, object?> item, CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(_configDictionary))
        {
            try
            {
                Dictionary<string, object?> strippedItem = new(item.Where(kvp => !kvp.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase)));

                if (_destinationContainerProperties.DefaultTimeToLive.HasValue && _destinationContainerProperties.DefaultTimeToLive.Value > 0)
                {
                    var destinationTTL = CalculateTTL(Convert.ToInt64(item["_ts"], CultureInfo.InvariantCulture), _destinationContainerProperties.DefaultTimeToLive.Value);
                    if (destinationTTL > 0)
                    {
                        strippedItem["ttl"] = destinationTTL;
                    }
                    else
                    {
                        using (_logger.BeginScope(_configDictionary))
                        {
                            _logger.NonPositiveTTLWarning(item["id"]?.ToString() ?? string.Empty);
                            return;
                        }
                    }
                }

                await _destinationContainer.UpsertItemAsync(strippedItem, cancellationToken: cancellationToken);
                if (item.ContainsKey("replication_status"))
                {
                    // If the item was previously marked as failed, we remove the replication fields
                    await RemoveErrorDetailsFromSourceDocumentAsync(item, cancellationToken);
                }
            }
            catch (CosmosException cex) when (cex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.RateLimitExceeded(cex, item["id"]?.ToString() ?? string.Empty);
                await StopAsync();
            }
            catch (Exception ex)
            {
                _logger.UpsertItemError(ex, item["id"]?.ToString() ?? string.Empty);
                if (item.TryGetValue("replication_attempts", out var attemptsObj) && attemptsObj is int attempts && attempts >= 3)
                {
                    _logger.ReplicationAttemptsExceeded(item["id"]?.ToString() ?? string.Empty);
                    return;
                }
                await PatchSourceDocumentWithErrorDetailsAsync(item, ex, cancellationToken);
            }
        }
    }

    private async Task RemoveErrorDetailsFromSourceDocumentAsync(IDictionary<string, object?> item, CancellationToken cancellationToken)
    {
        try
        {
            var partitionKey = ComputePartitionKey(item, _sourceContainerProperties);
            var patchOperations = new PatchOperation[]
            {
            PatchOperation.Remove("/replication_status"),
            PatchOperation.Remove("/replication_timestamp"),
            PatchOperation.Remove("/replication_error_details"),
            PatchOperation.Remove("/replication_attempts")
            };

            await _sourceContainer.PatchItemStreamAsync(item["id"] as string, partitionKey, patchOperations, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.RemoveReplicationFieldsError(ex, item["id"]?.ToString() ?? string.Empty);
        }
    }

    private static PartitionKey ComputePartitionKey(IDictionary<string, object?> item, ContainerProperties containerProperties)
    {
        // No partition key defined (single partition container)
        if (containerProperties.PartitionKeyPaths == null || containerProperties.PartitionKeyPaths.Count == 0)
        {
            return PartitionKey.None;
        }

        var builder = new PartitionKeyBuilder();

        foreach (var path in containerProperties.PartitionKeyPaths)
        {
            var key = path.TrimStart('/');
            item.TryGetValue(key, out var value);

            // PartitionKey.Null for explicit null
            if (value == null)
            {
                builder.AddNullValue();
            }
            else if (value is string s)
            {
                builder.Add(s);
            }
            else if (value is bool b)
            {
                builder.Add(b);
            }
            else if (value is double d)
            {
                builder.Add(d);
            }
            else if (value is float f)
            {
                builder.Add(f);
            }
            else if (value is int i)
            {
                builder.Add(i);
            }
            else if (value is long l)
            {
                builder.Add(l);
            }
            else
            {
                // fallback to string representation
                builder.Add(value.ToString());
            }
        }

        return builder.Build();
    }

    private async Task PatchSourceDocumentWithErrorDetailsAsync(IDictionary<string, object?> item, Exception ex, CancellationToken cancellationToken)
    {
        try
        {
            var partitionKey = ComputePartitionKey(item, _sourceContainerProperties);

            var patchOperations = new PatchOperation[]
            {
            PatchOperation.Set("/replication_status", "Failed"),
            PatchOperation.Set("/replication_timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            PatchOperation.Set("/replication_error_details", ex.ToString()),
            PatchOperation.Increment("/replication_attempts", 1)
            };

            await _sourceContainer.PatchItemStreamAsync(
                id: item["id"] as string,
                partitionKey: partitionKey,
                patchOperations: patchOperations,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception updateEx)
        {
            _logger.UpdateSourceDocumentError(updateEx, item["id"]?.ToString() ?? string.Empty);
        }
    }

    private static long CalculateTTL(long sourceTimestamp, int defaultTTL)
    {
        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var currentAge = currentTimestamp - sourceTimestamp;
        return defaultTTL - currentAge;
    }

}
