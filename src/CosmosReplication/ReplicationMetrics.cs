using System.Diagnostics.Metrics;

using CosmosReplication.Interfaces;
using CosmosReplication.Models;
using Microsoft.Extensions.Options;

namespace CosmosReplication;

public class ReplicationMetrics : IReplicationMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Gauge<long> _estimatedPendingChanges;
    private bool _disposedValue;

    public ReplicationMetrics(IMeterFactory meterFactory, IOptions<ReplicationConfiguration> options)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value);

        _meter = meterFactory.Create("CosmosReplication");
        _estimatedPendingChanges = _meter.CreateGauge<long>(
            name: "cosmosreplication.estimated_pending_changes",
            description: "The estimated number of pending changes to be replicated.",
            tags:
            [
                new KeyValuePair<string, object?>("replication_name", options.Value.Name)
            ]);
    }

    public void RecordEstimatedPendingChanges(long count, KeyValuePair<string, object?>[] tags) => _estimatedPendingChanges.Record(value: count, tags: tags);

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _meter.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }
}
