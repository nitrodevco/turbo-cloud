using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Observers;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain : IGrainWithIntegerKey
{
    public Task RegisterSessionObserverAsync(ISessionContextObserver observer);
    public Task UnregisterSessionObserverAsync(CancellationToken ct);
    public Task SendComposerAsync(IComposer composer);
    public Task SendComposerAsync(params IComposer[] composers);
    public Task<bool> HasActiveSessionAsync();
}
