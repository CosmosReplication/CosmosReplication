namespace CosmosReplication.Interfaces;

public interface IReplicationEstimatorService
{
  public Task StartAsync(CancellationToken cancellationToken);

  public Task StopAsync();
}
