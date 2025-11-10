using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Abstractions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Networking;

namespace Turbo.Messages;

public sealed class MessageSystem(MessageRegistry registry)
{
    private readonly MessageRegistry _registry = registry;

    public async Task PublishAsync(IMessageEvent env, ISessionContext meta, CancellationToken ct)
    {
        if (_registry is null)
            return;

        await _registry.PublishAsync(env, meta, ct).ConfigureAwait(false);
    }
}
