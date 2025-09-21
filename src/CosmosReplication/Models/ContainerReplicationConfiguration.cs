using System.ComponentModel.DataAnnotations;

namespace CosmosReplication.Models;

public class ContainerReplicationConfiguration
{
  [Required]
  public required string SourceAccount { get; set; }

  [Required]
  public required string SourceDatabase { get; set; }

  [Required]
  public required string SourceContainer { get; set; }

  [Required]
  public required string DestinationAccount { get; set; }

  [Required]
  public required string DestinationDatabase { get; set; }

  [Required]
  public required string DestinationContainer { get; set; }

  [Required]
  public required string LeaseAccount { get; set; }

  [Required]
  public required string LeaseDatabase { get; set; }

  [Required]
  public required string LeaseContainer { get; set; }

  public int? BatchSize { get; set; }

  public int? MaxItemCount { get; set; }

  public TimeSpan? PollInterval { get; set; }

  public DateTime? StartTime { get; set; }
}
