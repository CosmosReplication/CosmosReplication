namespace CosmosReplication.Models;

public class ContainerReplicationConfiguration
{
    public required string SourceAccount { get; set; }
    public required string SourceDatabase { get; set; }
    public required string SourceContainer { get; set; }
    public required string DestinationAccount { get; set; }
    public required string DestinationDatabase { get; set; }
    public required string DestinationContainer { get; set; }
    public required string LeaseAccount { get; set; }
    public required string LeaseDatabase { get; set; }
    public required string LeaseContainer { get; set; }

    public int? BatchSize { get; set; }
    public int? MaxItemCount { get; set; }
    public TimeSpan? PollInterval { get; set; }
    public DateTime? StartTime { get; set; }
}
