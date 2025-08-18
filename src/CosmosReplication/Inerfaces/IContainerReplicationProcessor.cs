namespace CosmosReplication.Interfaces;

public interface IContainerReplicationProcessor
{
    public bool Initialized { get; }
    public bool Started { get; }
    public Task<bool> InitializeAsync(CancellationToken cancellationToken);
    public Task StartAsync();
    public Task StopAsync();
}
