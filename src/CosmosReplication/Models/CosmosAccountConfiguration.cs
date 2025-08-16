namespace CosmosReplication.Models;

public class CosmosAccountConfiguration
{
    public required string AccountName { get; set; }
    public string? AccountEndpoint { get; set; }
    public string? ConnectionString { get; set; }
}
