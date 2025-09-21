namespace CosmosReplication.Interfaces;

public interface IContainerReplicationEstimator
{
  public bool Initialized { get; }

  public bool Started { get; }

  public Task<bool> InitializeAsync(CancellationToken cancellationToken);

  public Task StartAsync();

  public Task StopAsync();
}
