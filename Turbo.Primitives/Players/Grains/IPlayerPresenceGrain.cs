using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Players.Grains;

/// <summary>
/// The hub every other grain reports to, which is why it is the grain most likely to close a
/// deadlock: nearly every grain awaits it, and it awaits the room, the player and the messenger.
/// It is the transport, not the place that knows every domain's packets: a grain sends its own
/// composers with <c>SendComposerToPlayerAsync</c>, and a method here exists only when this
/// grain's state (the active room, the pending entry) decides what is sent or where.
///
/// Two kinds of method, and a method is one or the other:
/// <list type="bullet">
/// <item>
/// <b>Tells</b> — sends, notifications, reads of a field. <c>[AlwaysInterleave]</c>, and they
/// never await a grain: anything they pass on to a room or another grain is
/// <c>LogAndForget</c>. Any grain may await these, even while this grain is waiting on it.
/// </item>
/// <item>
/// <b>Flows</b> — entering and leaving a room, the session lifecycle. Not
/// interleaved, and they await other grains. Only handlers and this grain's own session drive
/// them; no grain awaits one.
/// </item>
/// </list>
/// A new method that another grain will await must be a tell.
/// </summary>
public partial interface IPlayerPresenceGrain : IGrainWithIntegerKey
{
    public Task RegisterSessionObserverAsync(
        SessionKey sessionKey,
        ISessionContextObserver observer,
        CancellationToken ct
    );
    public Task UnregisterSessionObserverAsync(SessionKey sessionKey, CancellationToken ct);

    [AlwaysInterleave]
    public Task SendComposerAsync(IComposer composer, CancellationToken ct);

    /// <summary>Atomically accepts a composer only for an attached session. False means offline;
    /// true means queued, not acknowledged. The queue is discarded when that session changes.</summary>
    [AlwaysInterleave]
    public Task<bool> TrySendComposerAsync(IComposer composer, CancellationToken ct);

    /// <summary>As <see cref="TrySendComposerAsync"/>, but only while <paramref name="sessionKey"/>
    /// is the attached session: for state one connection's client holds, which the next
    /// connection starts without. False means that session has gone.</summary>
    [AlwaysInterleave]
    public Task<bool> TrySendComposerToSessionAsync(
        SessionKey sessionKey,
        IComposer composer,
        CancellationToken ct
    );

    [AlwaysInterleave]
    public Task SendComposerAsync(IReadOnlyList<IComposer> composers, CancellationToken ct);

    /// <summary>
    /// Holds the composers other grains send this player from now on until a publish of the
    /// player's room carrying the returned hold arrives, the hold is released, or
    /// <c>PlayerConfig.OutboundHoldMs</c> passes. Room publishes are not held. For a grain that
    /// tells the room to send something without awaiting it and then sends the player its own
    /// composers, which must follow the room's. <see cref="OutboundHold.None"/> when there is
    /// nothing to wait for (no session, no room, holds off).
    /// </summary>
    [AlwaysInterleave]
    public Task<OutboundHold> HoldForRoomAsync(CancellationToken ct);

    /// <summary>Ends a hold whose room publish will not come (the player was not in the room).</summary>
    [AlwaysInterleave]
    public Task ReleaseHoldAsync(OutboundHold hold, CancellationToken ct);

    /// <summary>
    /// Interleaved: the player grain asks this while activating, and the presence grain may be
    /// waiting on that same activation. It only reads whether a session is attached.
    /// </summary>
    [AlwaysInterleave]
    public Task<bool> HasActiveSessionAsync(CancellationToken ct);

    /// <summary>
    /// When the player's session began and since when its time online is not yet saved to their
    /// total (<c>players.online_ms</c>); both null when they are offline. Interleaved: it only reads.
    /// </summary>
    [AlwaysInterleave]
    public Task<PlayerOnlineClockSnapshot> GetOnlineClockAsync(CancellationToken ct);
}
