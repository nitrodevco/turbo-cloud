using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Operations.Commands;

/// <summary>
/// A silence and a trade lock are not rows of their own: each is a denial of one permission node
/// on the player, which the permission grains expire, audit and tell the room about already.
/// </summary>
internal static class PermissionDenials
{
    public static Task<PermissionChangeResultType> DenyAsync(
        IGrainFactory grainFactory,
        PlayerId player,
        string node,
        DateTime? expiresAtUtc,
        PlayerId? actor,
        CancellationToken ct
    ) =>
        grainFactory
            .GetPlayerPermissionGrain(player)
            .SetNodeAsync(node, false, expiresAtUtc, PermissionExpiryModeType.Replace, actor, ct);

    /// <summary>
    /// Removes the denial, whether it was given for a time or for good; false when there was none.
    /// </summary>
    public static async Task<bool> LiftAsync(
        IGrainFactory grainFactory,
        PlayerId player,
        string node,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        var grain = grainFactory.GetPlayerPermissionGrain(player);
        var temporary = await grain.UnsetNodeAsync(node, true, actor, ct);
        var permanent = await grain.UnsetNodeAsync(node, false, actor, ct);

        return temporary == PermissionChangeResultType.Changed
            || permanent == PermissionChangeResultType.Changed;
    }
}
