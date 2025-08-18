namespace CosmosReplication.Interfaces;

public interface IReplicationMetrics
{
	public void RecordEstimatedPendingChanges(long count, KeyValuePair<string, object?>[] tags);
}
