using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Pipeline.Delegates;
using Turbo.Primitives.Orleans;

namespace Turbo.Messages.Registry;

/// <summary>
/// The gates every handler of a client's packet is wrapped in as it is registered, a packet
/// handler's or a plugin message handler's alike: <see cref="SignInGate"/> first, then
/// <see cref="PermissionGate"/>. The permission check asks the host's grain factory: a plugin's
/// own container has none.
/// </summary>
internal static class MessageGates
{
    public static HandlerInvoker<MessageContext> Wrap(
        Type handlerType,
        IServiceProvider host,
        ILogger signInLogger,
        HandlerInvoker<MessageContext> invoker
    )
    {
        // Resolved on the first gated packet, not here, which can run while the host is built.
        IGrainFactory? grainFactory = null;

        // Signed in first: a session that has not logged in is dropped before a permission is
        // asked about.
        return SignInGate.Wrap(
            handlerType,
            signInLogger,
            PermissionGate.Wrap(
                handlerType,
                (playerId, node, ct) =>
                    (grainFactory ??= host.GetRequiredService<IGrainFactory>()).HasPermissionAsync(
                        playerId,
                        node,
                        ct
                    ),
                invoker
            )
        );
    }
}
