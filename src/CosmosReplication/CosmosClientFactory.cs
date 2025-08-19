using Azure.Identity;

using CosmosReplication.Interfaces;
using CosmosReplication.Models;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace CosmosReplication;

public class CosmosClientFactory : ICosmosClientFactory
{
	private readonly Dictionary<string, CosmosAccountConfiguration> _cosmosAccountConfigurations;
	private readonly Dictionary<string, CosmosClient> _cosmosClients;
	private readonly CosmosClientOptions _cosmosClientOptions;

	public CosmosClientFactory(IOptions<ReplicationConfiguration> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(options.Value);

		_cosmosAccountConfigurations = options.Value.CosmosAccounts.ToDictionary(x => x.AccountName);
		_cosmosClients = [];
		_cosmosClientOptions = new CosmosClientOptions
		{
			ConnectionMode = ConnectionMode.Gateway,
			EnableContentResponseOnWrite = false,
			AllowBulkExecution = true,
		};
	}

	public CosmosClient GetCosmosClient(string accountName)
	{
		if (_cosmosClients.TryGetValue(accountName, out var cosmosClient))
		{
			return cosmosClient;
		}

		if (_cosmosAccountConfigurations.TryGetValue(accountName, out var options))
		{
			cosmosClient = GenerateClient(options);
			_cosmosClients[accountName] = cosmosClient;
			return cosmosClient;
		}

		throw new ArgumentException($"No CosmosAccount found for AccountName: {accountName}");
	}

	private CosmosClient GenerateClient(CosmosAccountConfiguration options) => !string.IsNullOrEmpty(options.ConnectionString)
			? new CosmosClient(options.ConnectionString, _cosmosClientOptions)
			: new CosmosClient(options.AccountEndpoint, new DefaultAzureCredential(), _cosmosClientOptions);
}
