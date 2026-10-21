using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Pipeline.Delegates;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Messages.Registry;

/// <summary>
/// Keeps every packet handler from a session that has not logged in: a packet from a session with
/// no signed-in player is dropped without a reply, after the behaviours and before the handler is
/// called, unless the handler carries <see cref="AllowAnonymousAttribute"/> (the handshake, the
/// SSO ticket, pings). Applied once, as the handler is registered, the same way as
/// <see cref="PermissionGate"/>; a handler with the attribute keeps its invoker untouched. See
/// <c>docs/permissions.md</c> §11.
/// </summary>
public static class SignInGate
{
    public static HandlerInvoker<MessageContext> Wrap(
        Type handlerType,
        ILogger logger,
        HandlerInvoker<MessageContext> invoker
    )
    {
        if (handlerType.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            return invoker;

        return (inst, env, ctx, ct) =>
        {
            if (ctx.PlayerId > 0)
                return invoker(inst, env, ctx, ct);

            logger.LogDebug(
                "Dropped {Message} from session {Session}: it has no signed-in player",
                env.GetType().Name,
                ctx.SessionKey
            );

            return ValueTask.CompletedTask;
        };
    }
}
