using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Abstractions;
using Turbo.Events.Registry;
using Turbo.Pipeline.Core;

namespace Turbo.Events;

public class EventSystem(GenericBus<IEvent, EventContext, object> bus)
{
    private readonly GenericBus<IEvent, EventContext, object> _bus = bus;

    public IDisposable RegisterFromAssembly(
        string ownerId,
        Assembly asm,
        IServiceProvider ownerRoot,
        bool useAmbientScope
    ) => _bus.RegisterFromAssembly(ownerId, asm, ownerRoot, useAmbientScope);

    public Task PublishAsync<T>(T envelope, CancellationToken ct = default)
        where T : IEvent => _bus.PublishAsync(envelope, true, ct);
}
