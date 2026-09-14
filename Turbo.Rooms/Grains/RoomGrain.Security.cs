using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<RoomControllerType> GetControllerLevelAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        return await SecurityModule.GetControllerLevelAsync(playerId);
    }

    public async Task<ImmutableArray<RoomControllerSnapshot>?> GetControllersAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        await SecurityModule.EnsureRightsLoadedAsync(ct);

        var controllerLevel = await SecurityModule.GetControllerLevelAsync(ctx);

        if (controllerLevel < RoomControllerType.Owner)
            return null;

        var playerIds = _state.PlayerIdsWithRights.ToList();

        if (playerIds.Count == 0)
            return ImmutableArray<RoomControllerSnapshot>.Empty;

        var names = await _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync(playerIds, ct);

        return playerIds
            .Select(playerId => new RoomControllerSnapshot
            {
                PlayerId = playerId,
                Name = names.TryGetValue(playerId, out var name) ? name : string.Empty,
            })
            .ToImmutableArray();
    }

    public async Task RefreshControllerLevelForPlayerAsync(ActionContext ctx, CancellationToken ct)
    {
        try
        {
            await SecurityModule.RefreshControllerLevelForPlayerAsync(ctx.PlayerId, ct);
        }
        catch
        {
            // TODO handle exceptions
        }
    }

    public async Task GiveRightsToPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        try
        {
            await SecurityModule.GiveRightsToPlayerAsync(ctx, playerId, ct);
        }
        catch
        {
            // TODO handle exceptions
        }
    }

    public async Task RemoveRightsFromPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        try
        {
            await SecurityModule.RemoveRightsFromPlayerAsync(ctx, playerId, ct);
        }
        catch
        {
            // TODO handle exceptions
        }
    }

    public async Task RemoveAllRightsAsync(ActionContext ctx, CancellationToken ct)
    {
        try
        {
            await SecurityModule.RemoveAllRightsAsync(ctx, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to remove all rights by {PlayerId} in room {RoomId}",
                ctx.PlayerId,
                _state.RoomId
            );
        }
    }
}
