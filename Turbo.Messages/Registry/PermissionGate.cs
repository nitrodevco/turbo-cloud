using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Pipeline.Delegates;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Messages.Registry;

/// <summary>
/// Enforces <see cref="RequiresPermissionAttribute"/> at the packet boundary: a handler that
/// declares it is reached only by a signed-in player holding any one of its nodes, and is
/// otherwise skipped without a reply. Applied once, as the handler is registered; a handler
/// without the attribute keeps its invoker untouched. See <c>docs/permissions.md</c> §11.
/// </summary>
public static class PermissionGate
{
    /// <summary>Whether a player holds a node: <c>IGrainFactory.HasPermissionAsync</c> in the server.</summary>
    public delegate Task<bool> PermissionCheck(
        PlayerId playerId,
        string node,
        CancellationToken ct
    );

    public static HandlerInvoker<MessageContext> Wrap(
        Type handlerType,
        PermissionCheck hasPermission,
        HandlerInvoker<MessageContext> invoker
    )
    {
        if (
            handlerType.GetCustomAttribute<RequiresPermissionAttribute>()
            is not { Nodes.Count: > 0 } required
        )
            return invoker;

        ImmutableArray<string> nodes = [.. required.Nodes];

        return async (inst, env, ctx, ct) =>
        {
            if (!await HoldsAnyAsync(hasPermission, ctx.PlayerId, nodes, ct).ConfigureAwait(false))
                return;

            await invoker(inst, env, ctx, ct).ConfigureAwait(false);
        };
    }

    private static async Task<bool> HoldsAnyAsync(
        PermissionCheck hasPermission,
        PlayerId playerId,
        ImmutableArray<string> nodes,
        CancellationToken ct
    )
    {
        if (playerId <= 0)
            return false;

        foreach (var node in nodes)
        {
            if (await hasPermission(playerId, node, ct).ConfigureAwait(false))
                return true;
        }

        return false;
    }
}
