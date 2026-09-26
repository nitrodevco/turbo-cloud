using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Observers;

namespace Turbo.Primitives.Players.Grains;

/// <summary>
/// The hub every other grain reports to, which is why it is the grain most likely to close a
/// deadlock: nearly every grain awaits it, and it awaits the room, the player, the messenger and
/// the inventory.
///
/// Two kinds of method, and a method is one or the other:
/// <list type="bullet">
/// <item>
/// <b>Tells</b> — sends, notifications, reads of a field. <c>[AlwaysInterleave]</c>, and they
/// never await a grain: anything they pass on to a room or another grain is
/// <c>LogAndForget</c>. Any grain may await these, even while this grain is waiting on it.
/// </item>
/// <item>
/// <b>Flows</b> — entering and leaving a room, opening an inventory, the session lifecycle. Not
/// interleaved, and they await other grains. Only handlers and this grain's own session drive
/// them; no grain awaits one.
/// </item>
/// </list>
/// A new method that another grain will await must be a tell.
/// </summary>
public partial interface IPlayerPresenceGrain : IGrainWithIntegerKey
{
    public Task RegisterSessionObserverAsync(
        ISessionContextObserver observer,
        CancellationToken ct
    );
    public Task UnregisterSessionObserverAsync(CancellationToken ct);

    [AlwaysInterleave]
    public Task SendComposerAsync(IComposer composer, CancellationToken ct);

    [AlwaysInterleave]
    public Task SendComposerAsync(IReadOnlyList<IComposer> composers, CancellationToken ct);

    /// <summary>
    /// Interleaved: the player grain asks this while activating, and the presence grain may be
    /// waiting on that same activation. It only reads whether a session is attached.
    /// </summary>
    [AlwaysInterleave]
    public Task<bool> HasActiveSessionAsync(CancellationToken ct);
}
