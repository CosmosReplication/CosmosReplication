namespace CosmosReplication.Inerfaces;
using Microsoft.Azure.Cosmos;

public interface ICosmosClientFactory
{
    CosmosClient GetCosmosClient(string accountName);
}
