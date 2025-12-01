using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Orleans.Observers;

public interface ISessionContextObserver : IGrainObserver
{
    public Task SendComposerAsync(IComposer composer, CancellationToken ct = default);
}
