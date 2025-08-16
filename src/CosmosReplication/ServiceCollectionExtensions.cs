namespace CosmosReplication;
using System;

using CosmosReplication.Inerfaces;
using CosmosReplication.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCosmosReplication(
        this IServiceCollection services,
        ReplicationConfiguration replicationConfiguration)
    {
        ArgumentNullException.ThrowIfNull(replicationConfiguration);
        services.AddSingleton(replicationConfiguration);
        services.AddSingleton<ICosmosClientFactory, CosmosClientFactory>();
        foreach (var config in replicationConfiguration.ContainerReplications)
        {
            services.AddSingleton(provider =>
            {
                var factory = provider.GetRequiredService<ICosmosClientFactory>();
                var logger = provider.GetRequiredService<ILogger<ContainerReplication>>();
                return new ContainerReplication(logger, factory, replicationConfiguration.Name, config);
            });
        }
        services.AddHostedService<ReplicationService>();
        return services;
    }

}
