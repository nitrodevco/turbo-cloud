using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Observers;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain : IGrainWithIntegerKey
{
    public Task RegisterSessionObserverAsync(
        ISessionContextObserver observer,
        CancellationToken ct
    );
    public Task UnregisterSessionObserverAsync(CancellationToken ct);
    public Task SendComposerAsync(IComposer composer, CancellationToken ct);
    public Task SendComposerAsync(IReadOnlyList<IComposer> composers, CancellationToken ct);

    /// <summary>
    /// Interleaved: the player grain asks this while activating, and the presence grain may be
    /// waiting on that same activation. It only reads whether a session is attached.
    /// </summary>
    [AlwaysInterleave]
    public Task<bool> HasActiveSessionAsync(CancellationToken ct);
}
