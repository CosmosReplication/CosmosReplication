using System.Diagnostics.Metrics;

using CosmosReplication.Interfaces;
using CosmosReplication.Models;

namespace CosmosReplication;

public class ReplicationMetrics : IReplicationMetrics, IDisposable
{
	private readonly Meter _meter;
	private readonly Gauge<long> _estimatedPendingChanges;
	private bool _disposedValue;

	public ReplicationMetrics(IMeterFactory meterFactory, ReplicationConfiguration replicationConfiguration)
	{
		ArgumentNullException.ThrowIfNull(replicationConfiguration);
		_meter = meterFactory.Create("CosmosReplication");
		_estimatedPendingChanges = _meter.CreateGauge<long>(
			name: "cosmosreplication.estimated_pending_changes",
			description: "The estimated number of pending changes to be replicated.",
			tags:
			[
				new KeyValuePair<string, object?>("replication_name", replicationConfiguration.Name)
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
