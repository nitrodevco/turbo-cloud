using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Grains.Observers;

public interface ISessionContextObserver : IGrainObserver
{
    public Task SendComposerAsync(IComposer composer, CancellationToken ct = default);
}
