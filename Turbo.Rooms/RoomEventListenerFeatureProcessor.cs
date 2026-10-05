using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Rooms;
using Turbo.Rooms.Grains;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms;

/// <summary>
/// Registers the <see cref="IRoomEventListener"/>s an assembly declares, so a plugin can watch
/// room events while it is loaded and stops hearing them when it unloads. A listener is built
/// once with the plugin's services and must be public to be found; one that is
/// <see cref="IDisposable"/> is disposed when the plugin unloads. The room's own systems also
/// implement the interface but belong to a grain and are built by it, so they are skipped.
/// </summary>
internal sealed class RoomEventListenerFeatureProcessor(IRoomEventListenerRegistry registry)
    : IAssemblyFeatureProcessor
{
    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var listeners = AssemblyExplorer
            .FindAssignees(asm, typeof(IRoomEventListener))
            .Where(concrete => !typeof(RoomGrainComponent).IsAssignableFrom(concrete))
            .Select(concrete => (IRoomEventListener)ActivatorUtilities.CreateInstance(sp, concrete))
            .ToList();

        var registrations = new CompositeDisposable();

        if (listeners.Count == 0)
            return Task.FromResult<IDisposable>(registrations);

        // Disposed in reverse: the registration goes first, so nothing is published to a listener
        // as it is being disposed.
        registrations.Add(listeners.OfType<IDisposable>());
        registrations.Add(registry.Register(listeners));

        return Task.FromResult<IDisposable>(registrations);
    }
}
