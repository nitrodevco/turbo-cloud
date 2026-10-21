using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// Runs a camera capture (the render data stored, drawn by <see cref="ICameraRenderer"/>, and the
/// answer built) off the packet that asked for it, and sends the answer to the player when it is
/// done. A session's packets are handled one at a time, and a render waits for one of
/// <see cref="CameraConfig.MaxConcurrentRenders"/> hotel-wide slots and runs for up to
/// <see cref="CameraConfig.RendererTimeoutMilliseconds"/>: awaited in the handler, it held up
/// everything else the player sent meanwhile (walking, chat, their next room). One capture per
/// player runs at a time, as when their session serialized them; another is refused while it runs,
/// so one player cannot hold every slot or queue renders without bound.
/// </summary>
public sealed class CameraCaptureJobs(
    IGrainFactory grainFactory,
    IHostApplicationLifetime lifetime,
    ILogger<CameraCaptureJobs> logger
)
{
    private readonly ConcurrentDictionary<int, byte> _running = new();

    /// <summary>
    /// Starts <paramref name="capture"/> for <paramref name="playerId"/> and returns at once; the
    /// composer it returns is sent to the player. False, and nothing started, when the player
    /// already has a capture running.
    /// </summary>
    public bool TryStart(PlayerId playerId, Func<CancellationToken, Task<IComposer>> capture)
    {
        if (!_running.TryAdd(playerId.Value, 0))
            return false;

        // Not the packet's token: the capture outlives the handler that started it, and stops
        // with the server.
        Task.Run(
                () => RunAsync(playerId, capture, lifetime.ApplicationStopping),
                CancellationToken.None
            )
            .LogAndForget(logger, "finish a camera capture for player {PlayerId}", playerId.Value);

        return true;
    }

    private async Task RunAsync(
        PlayerId playerId,
        Func<CancellationToken, Task<IComposer>> capture,
        CancellationToken ct
    )
    {
        IComposer answer;

        try
        {
            answer = await capture(ct).ConfigureAwait(false);
        }
        finally
        {
            // Before the answer goes out, so the player's next capture, sent as soon as it
            // arrives, is not refused as one still running.
            _running.TryRemove(playerId.Value, out _);
        }

        await grainFactory.SendComposerToPlayerAsync(playerId, answer, ct).ConfigureAwait(false);
    }
}
