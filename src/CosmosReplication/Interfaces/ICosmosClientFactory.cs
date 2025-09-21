using Microsoft.Azure.Cosmos;

namespace CosmosReplication.Interfaces;

public interface ICosmosClientFactory
{
  public CosmosClient GetCosmosClient(string accountName);
}
