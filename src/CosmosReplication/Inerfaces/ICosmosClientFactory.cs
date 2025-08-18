namespace CosmosReplication.Interfaces;

using Microsoft.Azure.Cosmos;

public interface ICosmosClientFactory
{
    public CosmosClient GetCosmosClient(string accountName);
}
