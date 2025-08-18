using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;

using CosmosReplication.Interfaces;
using CosmosReplication.Models;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace CosmosReplication;

public class ContainerReplicationProcessor : IContainerReplicationProcessor
{
	private readonly ILogger<ContainerReplicationProcessor> _logger;

	private readonly ReadOnlyCollection<KeyValuePair<string, object?>> _configDictionary;

	private readonly ICosmosClientFactory _cosmosClientFactory;

	private readonly string _replicationName;

	private readonly ContainerReplicationConfiguration _config;

	private ChangeFeedProcessor _changeFeedProcessor = null!;

	private Container _sourceContainer = null!;

	private ContainerProperties _sourceContainerProperties = null!;

	private Container _destinationContainer = null!;

	private ContainerProperties _destinationContainerProperties = null!;

	public ContainerReplicationProcessor(
		ILogger<ContainerReplicationProcessor> logger,
		ICosmosClientFactory cosmosClientFactory,
		string replicationName,
		ContainerReplicationConfiguration config)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

	public bool Started { get; private set; }

	public bool Initialized { get; private set; }

	public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
	{
		using (_logger.BeginScope(_configDictionary))
		{
			if (!Initialized)
			{
				try
				{
					_logger.ServiceInitializing(nameof(ContainerReplicationProcessor));

					var sourceContainer = _cosmosClientFactory.GetCosmosClient(_config.SourceAccount)
						.GetContainer(_config.SourceDatabase, _config.SourceContainer);
					var leaseContainer = _cosmosClientFactory.GetCosmosClient(_config.LeaseAccount)
						.GetContainer(_config.LeaseDatabase, _config.LeaseContainer);
					var destinationContainer = _cosmosClientFactory.GetCosmosClient(_config.DestinationAccount)
						.GetContainer(_config.DestinationDatabase, _config.DestinationContainer);

					// Checks if all required containers exist.
					var sourceContainerProperties = (await sourceContainer.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Resource;
					await leaseContainer.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
					var destinationContainerProperties = (await destinationContainer.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Resource;

					_changeFeedProcessor = sourceContainer
						.GetChangeFeedProcessorBuilder<IDictionary<string, object?>>(_replicationName, MigrateChangesAsync)
						.WithInstanceName(Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName)
						.WithLeaseContainer(leaseContainer)
						.WithMaxItems(_config.MaxItemCount ?? 100)
						.WithPollInterval(_config.PollInterval ?? TimeSpan.FromSeconds(5))
						.WithStartTime(_config.StartTime ?? DateTime.MinValue.ToUniversalTime())
						.WithErrorNotification(OnErrorAsync)
						.Build();

					_sourceContainer = sourceContainer;
					_sourceContainerProperties = sourceContainerProperties;
					_destinationContainer = destinationContainer;
					_destinationContainerProperties = destinationContainerProperties;
					Initialized = true;

					_logger.ServiceInitialized(nameof(ContainerReplicationProcessor));
				}
				catch (Exception ex)
				{
					_logger.ServiceInitializationFailed(nameof(ContainerReplicationProcessor), ex);
				}
			}
			else
			{
				_logger.ServiceAlreadyInitialized(nameof(ContainerReplicationProcessor));
			}

			return Initialized;
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
					_logger.ServiceStarting(nameof(ContainerReplicationProcessor));
					await _changeFeedProcessor.StartAsync().ConfigureAwait(false);
					_logger.ServiceStarted(nameof(ContainerReplicationProcessor));
					Started = true;
				}
				catch (Exception ex)
				{
					_logger.ServiceStartFailed(nameof(ContainerReplicationProcessor), ex);
					Started = false;
				}
			}
			else
			{
				_logger.ServiceAlreadyStarted(nameof(ContainerReplicationProcessor));
			}
		}
	}

	public async Task StopAsync()
	{
		if (Initialized && Started)
		{
			using (_logger.BeginScope(_configDictionary))
			{
				_logger.ServiceStopping(nameof(ContainerReplicationProcessor));
				await _changeFeedProcessor.StopAsync().ConfigureAwait(false);
				_logger.ServiceStopped(nameof(ContainerReplicationProcessor));
				Started = false;
			}
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

	private static long CalculateTTL(long sourceTimestamp, int defaultTTL)
	{
		var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var currentAge = currentTimestamp - sourceTimestamp;
		return defaultTTL - currentAge;
	}

	private Task OnErrorAsync(string leaseToken, Exception exception)
	{
		using (_logger.BeginScope(_configDictionary))
		{
			_logger.LeaseProcessingError(exception, leaseToken);
			return Task.CompletedTask;
		}
	}

	private async Task MigrateChangesAsync(ChangeFeedProcessorContext context, IReadOnlyCollection<IDictionary<string, object?>> changes, CancellationToken cancellationToken)
	{
		if (_destinationContainer.Database.Client.ClientOptions.AllowBulkExecution
		&& _config.BatchSize.HasValue
		&& _config.BatchSize.Value > 1)
		{
			foreach (var chunk in changes.Chunk(_config.BatchSize.Value))
			{
				var tasks = chunk.Select(item => UpsertDocumentAsync(item, cancellationToken));
				await Task.WhenAll(tasks).ConfigureAwait(false);
			}
		}
		else
		{
			foreach (var item in changes)
			{
				await UpsertDocumentAsync(item, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private async Task UpsertDocumentAsync(IDictionary<string, object?> item, CancellationToken cancellationToken)
	{
		using (_logger.BeginScope(_configDictionary))
		{
			try
			{
				Dictionary<string, object?> strippedItem = new(item.Where(kvp => !kvp.Key.StartsWith('_')));

				if ((_destinationContainerProperties.DefaultTimeToLive ?? 0) > 0)
				{
					var destinationTTL = CalculateTTL(Convert.ToInt64(item["_ts"], CultureInfo.InvariantCulture), _destinationContainerProperties.DefaultTimeToLive!.Value);
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

				await _destinationContainer.UpsertItemAsync(strippedItem, cancellationToken: cancellationToken).ConfigureAwait(false);
				if (item.ContainsKey("replication_status"))
				{
					// If the item was previously marked as failed, we remove the replication fields
					await RemoveErrorDetailsFromSourceDocumentAsync(item, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (TaskCanceledException tcex)
			{
				_logger.UpsertItemError(tcex, item["id"]?.ToString() ?? string.Empty);
				throw;
			}
			catch (OperationCanceledException ocex)
			{
				_logger.UpsertItemError(ocex, item["id"]?.ToString() ?? string.Empty);
				throw;
			}
			catch (CosmosException cex) when (cex.StatusCode == HttpStatusCode.TooManyRequests)
			{
				_logger.UpsertItemError(cex, item["id"]?.ToString() ?? string.Empty);
				throw;
			}
			catch (Exception ex)
			{
				_logger.UpsertItemError(ex, item["id"]?.ToString() ?? string.Empty);
				if (item.TryGetValue("replication_attempts", out var attemptsObj) && attemptsObj is int attempts && attempts >= 3)
				{
					_logger.ReplicationAttemptsExceeded(item["id"]?.ToString() ?? string.Empty);
					return;
				}

				await PatchSourceDocumentWithErrorDetailsAsync(item, ex, cancellationToken).ConfigureAwait(false);
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
				PatchOperation.Remove("/replication_attempts"),
			};

			await _sourceContainer.PatchItemStreamAsync(item["id"] as string, partitionKey, patchOperations, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.RemoveReplicationFieldsError(ex, item["id"]?.ToString() ?? string.Empty);
		}
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
				PatchOperation.Increment("/replication_attempts", 1),
			};

			await _sourceContainer.PatchItemStreamAsync(
				id: item["id"] as string,
				partitionKey: partitionKey,
				patchOperations: patchOperations,
				cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception updateEx)
		{
			_logger.UpdateSourceDocumentError(updateEx, item["id"]?.ToString() ?? string.Empty);
		}
	}
}
