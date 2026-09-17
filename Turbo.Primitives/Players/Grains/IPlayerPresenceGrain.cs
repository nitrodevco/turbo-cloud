using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
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
    public Task<bool> HasActiveSessionAsync(CancellationToken ct);
}
