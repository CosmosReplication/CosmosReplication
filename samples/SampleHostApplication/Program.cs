using CosmosReplication;
using CosmosReplication.Models;

var builder = WebApplication.CreateBuilder(args);

var replicationConfiguration = builder.Configuration.GetSection("ReplicationConfiguration").Get<ReplicationConfiguration>() ?? throw new InvalidOperationException("ReplicationConfiguration section is missing or invalid.");
builder.Services.AddCosmosReplication(replicationConfiguration);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
