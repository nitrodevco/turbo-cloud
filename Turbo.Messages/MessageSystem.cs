using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Abstractions;
using Turbo.Messages.Registry;
using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline.Core;

namespace Turbo.Messages;

public class MessageSystem(GenericBus<IMessageEvent, MessageContext, ISessionContext> bus)
{
    private readonly GenericBus<IMessageEvent, MessageContext, ISessionContext> _bus = bus;

    public IDisposable RegisterFromAssembly(
        string ownerId,
        Assembly asm,
        IServiceProvider ownerRoot,
        bool useAmbientScope
    ) => _bus.RegisterFromAssembly(ownerId, asm, ownerRoot, useAmbientScope);

    public Task PublishAsync<T>(T envelope, ISessionContext session, CancellationToken ct = default)
        where T : IMessageEvent => _bus.PublishAsync(envelope, true, session, ct);
}
