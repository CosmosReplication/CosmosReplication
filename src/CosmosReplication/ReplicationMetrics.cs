namespace CosmosReplication;

using System.Diagnostics.Metrics;

using CosmosReplication.Interfaces;
using CosmosReplication.Models;

public class ReplicationMetrics : IReplicationMetrics
{
    private readonly Gauge<long> _estimatedPendingChanges;

    public ReplicationMetrics(IMeterFactory meterFactory, ReplicationConfiguration replicationConfiguration)
    {
        var meter = meterFactory.Create("CosmosReplication");
        _estimatedPendingChanges = meter.CreateGauge<long>(
            name: "cosmosreplication.estimated_pending_changes",
            description: "The estimated number of pending changes to be replicated.",
            tags:
            [
                new KeyValuePair<string, object?>("replication_name", replicationConfiguration.Name)
            ]);
    }

    public void RecordEstimatedPendingChanges(long count, KeyValuePair<string, object?>[] tags) => _estimatedPendingChanges.Record(value: count, tags: tags);
}
