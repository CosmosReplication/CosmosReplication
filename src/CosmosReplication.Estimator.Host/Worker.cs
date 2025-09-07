using System.Diagnostics;
using CosmosReplication.Interfaces;

using k8s;
using k8s.LeaderElection;
using k8s.LeaderElection.ResourceLock;
using Microsoft.VisualStudio.Threading;

namespace CosmosReplication.Estimator.Host;

public sealed class Worker : IHostedService, IDisposable
{
    private readonly ILogger<Worker> _logger;
    private readonly IReplicationEstimatorService _replicationEstimatorService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private LeaderElector? _leaderElector;
    private bool _disposedValue;
    private JoinableTaskContext? _joinableTaskContext;
    private JoinableTaskFactory? _joinableTaskFactory;
    private Kubernetes? _client;

    public Worker(ILogger<Worker> logger, IReplicationEstimatorService replicationEstimatorService, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _replicationEstimatorService = replicationEstimatorService ?? throw new ArgumentNullException(nameof(replicationEstimatorService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var replicationHostReadyUrl = _configuration["ReplicationHost:ReadyUrl"];
        var replicationHostReadyTimeout = TimeSpan.FromSeconds(_configuration.GetValue("ReplicationHost:ReadyTimeout", 60));

        if (!string.IsNullOrWhiteSpace(replicationHostReadyUrl) && !await WaitForReplicationReadyAsync(replicationHostReadyUrl, replicationHostReadyTimeout, cancellationToken))
        {
            _logger.ReplicationHostReadyTimeout(replicationHostReadyUrl, replicationHostReadyTimeout);
        }

        var electionEnabled = _configuration.GetValue("LeaderElection:Enabled", false);
        if (!electionEnabled)
        {
            _logger.LeaderElectionDisabled();
            await _replicationEstimatorService.StartAsync(cancellationToken);
            return;
        }

        try
        {
            _client = new Kubernetes(KubernetesClientConfiguration.InClusterConfig());
        }
        catch (Exception ex)
        {
            _logger.KubernetesClientCreationFailed(ex);
            await _replicationEstimatorService.StartAsync(cancellationToken);
            return;
        }

        _joinableTaskContext = new JoinableTaskContext();
        _joinableTaskFactory = new JoinableTaskFactory(_joinableTaskContext);

        var ns = Environment.GetEnvironmentVariable("POD_NAMESPACE") ?? _configuration["POD_NAMESPACE"] ?? "default";
        var podName = Environment.GetEnvironmentVariable("POD_NAME") ?? _configuration["POD_NAME"] ?? Guid.NewGuid().ToString("n");
        var leaseName = _configuration.GetValue("LeaderElection:LeaseName", "cosmosreplication-estimator");
        var leaseDuration = TimeSpan.FromSeconds(_configuration.GetValue("LeaderElection:LeaseDuration", 30));
        var renewDeadline = TimeSpan.FromSeconds(_configuration.GetValue("LeaderElection:RenewDeadline", 15));
        var retryPeriod = TimeSpan.FromSeconds(_configuration.GetValue("LeaderElection:RetryPeriod", 5));

        var identity = $"{podName}_{Guid.NewGuid():N}";
        var leaseLock = new LeaseLock(_client, ns, leaseName, identity);

        var config = new LeaderElectionConfig(leaseLock)
        {
            LeaseDuration = leaseDuration,
            RenewDeadline = renewDeadline,
            RetryPeriod = retryPeriod,
        };

        _leaderElector = new LeaderElector(config);
        _leaderElector.OnStartedLeading += OnStartedLeading;
        _leaderElector.OnStoppedLeading += OnStoppedLeading;
        await _leaderElector.RunAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken) => await _replicationEstimatorService.StopAsync();

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void OnStoppedLeading()
    {
        _logger.LostLeadership();
        _ = _joinableTaskFactory!.RunAsync(_replicationEstimatorService.StopAsync);
    }

    private void OnStartedLeading()
    {
        _logger.ElectedLeader();
        _ = _joinableTaskFactory!.RunAsync(async () => await _replicationEstimatorService.StartAsync(CancellationToken.None));
    }

    private async Task<bool> WaitForReplicationReadyAsync(string replicationHostReadyUrl, TimeSpan timeout, CancellationToken ct)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var replicationHostReadyUri = new Uri(replicationHostReadyUrl);
        var sw = Stopwatch.StartNew();
        while (!ct.IsCancellationRequested && sw.Elapsed < timeout)
        {
            try
            {
                using var resp = await httpClient.GetAsync(replicationHostReadyUri, ct);
                resp.EnsureSuccessStatusCode();
                _logger.ReplicationHostReadySuccess(replicationHostReadyUrl);
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.ReplicationHostReadyFailed(replicationHostReadyUrl, ex);
            }

            await Task.Delay(5000, ct);
        }

        return false;
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _leaderElector?.Dispose();
                _client?.Dispose();
                _joinableTaskContext?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }
}
