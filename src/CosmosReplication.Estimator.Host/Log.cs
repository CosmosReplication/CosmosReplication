namespace CosmosReplication.Estimator.Host;

public static partial class Log
{
    [LoggerMessage(LogLevel.Information, "Leader election disabled, starting replication estimator service directly.")]
    public static partial void LeaderElectionDisabled(this ILogger logger);

    [LoggerMessage(LogLevel.Error, "Failed to create in-cluster Kubernetes client, starting replication estimator service directly.")]
    public static partial void KubernetesClientCreationFailed(this ILogger logger, Exception? ex);

    [LoggerMessage(LogLevel.Information, "Lost leadership, stopping replication estimator service.")]
    public static partial void LostLeadership(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Elected leader, starting replication estimator service.")]
    public static partial void ElectedLeader(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Replication host ready endpoint returned success: {url}")]
    public static partial void ReplicationHostReadySuccess(this ILogger logger, string url);

    [LoggerMessage(LogLevel.Warning, "Failed to query replication host ready at {url}")]
    public static partial void ReplicationHostReadyFailed(this ILogger logger, string url, Exception ex);

    [LoggerMessage(LogLevel.Warning, "Timed out waiting for replication host ready URL {url} after {timeout}")]
    public static partial void ReplicationHostReadyTimeout(this ILogger logger, string url, TimeSpan timeout);
}
