using System.ComponentModel.DataAnnotations;

namespace CosmosReplication.Models;

public class CosmosAccountConfiguration
{
	[Required]
	public required string AccountName { get; set; }

	[Url]
	public string? AccountEndpoint { get; set; }

	public string? ConnectionString { get; set; }
}
