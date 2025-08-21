using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Core.Configuration;
using Turbo.Core.Networking;
using Turbo.Core.Networking.Behaviors;
using Turbo.Core.Networking.Dispatcher;
using Turbo.Core.Networking.Pipeline;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;
using Turbo.Networking.Behaviors;
using Turbo.Networking.Pipelines;

namespace Turbo.Networking.Dispatcher;

public class PacketDispatcher(
    ISessionManager sessionManager,
    IRevisionManager revisionManager,
    IPacketMessageHub messageHub,
    IPacketQueue queue,
    IIngressPipeline ingress,
    IEmulatorConfig config,
    ILogger<PacketDispatcher> logger
) : BackgroundService, IPacketDispatcher
{
    private readonly ISessionManager _sessionManager = sessionManager;
    private readonly IPacketQueue _queue = queue;
    private readonly IIngressPipeline _ingress = ingress;
    private readonly IEmulatorConfig _config = config;
    private readonly ILogger<PacketDispatcher> _logger = logger;

    private readonly IProcessingPipeline _processing = new ProcessingPipeline(
        [
            new SequencingBehavior(logger),
            new ExceptionHandlingBehavior(logger),
            new RevisionDispatchBehavior(revisionManager, messageHub, logger),
            new CleanupBehavior(ingress, logger),
        ]
    );

    // Health monitoring
    private readonly ConcurrentDictionary<int, WorkerHealth> _workerHealth = new();
    private volatile bool _isShuttingDown;

    private sealed class WorkerHealth
    {
        public long PacketsProcessed { get; set; }
        public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
        public Exception LastError { get; set; }
        public int ConsecutiveErrors { get; set; }
    }

    public bool TryEnqueue(
        IChannelId channelId,
        IClientPacket packet,
        out PacketRejectType rejectType
    )
    {
        var env = new PacketEnvelope(channelId, packet);
        var ok = _ingress.TryAccept(env, out rejectType, out _);
        return ok;
    }

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "Starting {WorkerCount} packet dispatcher workers",
            _queue.ShardCount
        );

        return Task.WhenAll(Enumerable.Range(0, _queue.ShardCount).Select(i => Worker(i, ct)));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _isShuttingDown = true;
        _logger.LogInformation("Shutting down packet dispatcher...");

        await base.StopAsync(cancellationToken);

        // Log final health statistics
        LogWorkerHealthSummary();
    }

    private async Task Worker(int shardIndex, CancellationToken ct)
    {
        var health = _workerHealth.GetOrAdd(shardIndex, _ => new WorkerHealth());
        var consecutiveErrors = 0;
        const int maxConsecutiveErrors = 10;

        _logger.LogDebug("Worker {ShardIndex} starting", shardIndex);

        try
        {
            await foreach (var env in _queue.ReadShardAsync(shardIndex, ct))
            {
                if (_isShuttingDown)
                    break;

                try
                {
                    if (!_sessionManager.TryGetSession(env.ChannelId, out var session))
                    {
                        _logger.LogDebug(
                            "Dropping packet for unknown session sid={Sid}",
                            env.ChannelId
                        );
                        continue;
                    }

                    var token = _ingress.GetOrCreateToken(env.ChannelId);
                    var ctx = new PacketContext(session, _logger, token);

                    // Process with timeout to prevent hanging
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 second timeout per packet

                    await _processing.ProcessAsync(ctx, env, timeoutCts.Token);

                    // Update health metrics on success
                    health.PacketsProcessed++;
                    health.LastActivityUtc = DateTime.UtcNow;
                    health.ConsecutiveErrors = 0;
                    consecutiveErrors = 0;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Expected during shutdown
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Packet processing timeout
                    _logger.LogWarning(
                        "Packet processing timeout for shard {ShardIndex}, packet header={Header}",
                        shardIndex,
                        env.Msg.Header
                    );

                    consecutiveErrors++;
                    health.ConsecutiveErrors++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error processing packet in worker {ShardIndex}, header={Header}",
                        shardIndex,
                        env.Msg.Header
                    );

                    health.LastError = ex;
                    consecutiveErrors++;
                    health.ConsecutiveErrors++;
                }

                // Circuit breaker: if too many consecutive errors, pause this worker briefly
                if (consecutiveErrors >= maxConsecutiveErrors)
                {
                    _logger.LogWarning(
                        "Worker {ShardIndex} has {ConsecutiveErrors} consecutive errors, pausing for 5 seconds",
                        shardIndex,
                        consecutiveErrors
                    );

                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    consecutiveErrors = 0; // Reset after pause
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex,
                "Worker {ShardIndex} crashed with unhandled exception",
                shardIndex
            );
            throw; // Re-throw to fail the service
        }
        finally
        {
            _logger.LogDebug(
                "Worker {ShardIndex} stopped, processed {PacketsProcessed} packets",
                shardIndex,
                health.PacketsProcessed
            );
        }
    }

    private void LogWorkerHealthSummary()
    {
        var totalPackets = _workerHealth.Values.Sum(h => h.PacketsProcessed);
        var workersWithErrors = _workerHealth.Values.Count(h => h.ConsecutiveErrors > 0);

        _logger.LogInformation(
            "Packet dispatcher shutdown complete. Total packets processed: {TotalPackets}, Workers with errors: {WorkersWithErrors}",
            totalPackets,
            workersWithErrors
        );

        foreach (var kvp in _workerHealth)
        {
            var health = kvp.Value;
            if (health.ConsecutiveErrors > 0 || health.LastError != null)
            {
                _logger.LogWarning(
                    "Worker {ShardIndex} final state: {PacketsProcessed} packets, {ConsecutiveErrors} consecutive errors, last error: {LastError}",
                    kvp.Key,
                    health.PacketsProcessed,
                    health.ConsecutiveErrors,
                    health.LastError?.Message
                );
            }
        }
    }

    public void ResetForChannelId(IChannelId channelId) => _ingress.Reset(channelId);

    /// <summary>
    /// Gets health information for all workers. Useful for monitoring and diagnostics.
    /// </summary>
    public WorkerHealthInfo[] GetWorkerHealthInfo()
    {
        return _workerHealth
            .Select(kvp => new WorkerHealthInfo
            {
                ShardIndex = kvp.Key,
                PacketsProcessed = kvp.Value.PacketsProcessed,
                LastActivityUtc = kvp.Value.LastActivityUtc,
                ConsecutiveErrors = kvp.Value.ConsecutiveErrors,
                LastErrorMessage = kvp.Value.LastError?.Message,
            })
            .ToArray();
    }

    /// <summary>
    /// Checks if all workers are healthy (no excessive errors, recent activity).
    /// </summary>
    public bool IsHealthy()
    {
        if (_isShuttingDown)
            return false;

        var now = DateTime.UtcNow;
        const int maxConsecutiveErrors = 5;
        var maxInactivityMinutes = 5;

        return _workerHealth.Values.All(health =>
            health.ConsecutiveErrors < maxConsecutiveErrors
            && (now - health.LastActivityUtc).TotalMinutes < maxInactivityMinutes
        );
    }

    /// <summary>
    /// Health information for a worker.
    /// </summary>
    public class WorkerHealthInfo
    {
        public int ShardIndex { get; init; }
        public long PacketsProcessed { get; init; }
        public DateTime LastActivityUtc { get; init; }
        public int ConsecutiveErrors { get; init; }
        public string LastErrorMessage { get; init; }
    }
}
