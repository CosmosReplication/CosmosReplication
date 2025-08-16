namespace CosmosReplication.Models;

public class ReplicationConfiguration
{
    public required string Name { get; set; }
    public required CosmosAccountConfiguration[] CosmosAccounts { get; set; }
    public required ContainerReplicationConfiguration[] ContainerReplications { get; set; }
}
