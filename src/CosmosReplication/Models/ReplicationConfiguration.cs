using System.ComponentModel.DataAnnotations;

namespace CosmosReplication.Models;

public class ReplicationConfiguration
{
	[Required]
	public required string Name { get; set; }

	[Required]
	public required IReadOnlyList<CosmosAccountConfiguration> CosmosAccounts { get; set; }

	[Required]
	public required IReadOnlyList<ContainerReplicationConfiguration> ContainerReplications { get; set; }
}
