namespace CosmosReplication;

using Microsoft.Extensions.Logging;

public static partial class Log
{
    [LoggerMessage(0, LogLevel.Information, "{ServiceName} is initializing")]
    public static partial void ServiceInitializing(this ILogger logger, string serviceName);

    [LoggerMessage(0, LogLevel.Information, "{ServiceName} has initialized.")]
    public static partial void ServiceInitialized(this ILogger logger, string serviceName);

    [LoggerMessage(0, LogLevel.Information, "{ServiceName} is starting")]
    public static partial void ServiceStarting(this ILogger logger, string serviceName);

    [LoggerMessage(0, LogLevel.Information, "{ServiceName} has started.")]
    public static partial void ServiceStarted(this ILogger logger, string serviceName);

    [LoggerMessage(0, LogLevel.Information, "{ServiceName} is stopping")]
    public static partial void ServiceStopping(this ILogger logger, string serviceName);

    [LoggerMessage(0, LogLevel.Information, "{ServiceName} has stopped.")]
    public static partial void ServiceStopped(this ILogger logger, string serviceName);

    [LoggerMessage(0, LogLevel.Warning, "{ServiceName} is already initialized")]
    public static partial void ServiceAlreadyInitialized(this ILogger logger, string serviceName);

    [LoggerMessage(0, LogLevel.Warning, "{ServiceName} is already started")]
    public static partial void ServiceAlreadyStarted(this ILogger logger, string serviceName);

    [LoggerMessage(0, LogLevel.Error, "{ServiceName} failed to initialize")]
    public static partial void ServiceInitializationFailed(this ILogger logger, string serviceName, Exception? ex);

    [LoggerMessage(0, LogLevel.Error, "{ServiceName} failed to start")]
    public static partial void ServiceStartFailed(this ILogger logger, string serviceName, Exception? ex);

    [LoggerMessage(1, LogLevel.Error, "Error occurred while processing lease {LeaseToken}.")]
    public static partial void LeaseProcessingError(this ILogger logger, Exception exception, string leaseToken);

    [LoggerMessage(2, LogLevel.Warning, "TTL for item with id {Id} is non-positive, skipping upsert.")]
    public static partial void NonPositiveTTLWarning(this ILogger logger, string id);

    [LoggerMessage(3, LogLevel.Error, "Rate limit exceeded while upserting item with id {Id}. Stopping migration...")]
    public static partial void RateLimitExceeded(this ILogger logger, Exception exception, string id);

    [LoggerMessage(4, LogLevel.Error, "Error occurred while upserting item with id {Id}.")]
    public static partial void UpsertItemError(this ILogger logger, Exception exception, string id);

    [LoggerMessage(5, LogLevel.Warning, "Replication attempts for item with id {Id} exceeded limit.")]
    public static partial void ReplicationAttemptsExceeded(this ILogger logger, string id);

    [LoggerMessage(6, LogLevel.Error, "Error occurred while removing replication fields for item with id {Id}.")]
    public static partial void RemoveReplicationFieldsError(this ILogger logger, Exception exception, string id);

    [LoggerMessage(7, LogLevel.Error, "Error occurred while updating source document with failure status for id {Id}.")]
    public static partial void UpdateSourceDocumentError(this ILogger logger, Exception exception, string id);

}
