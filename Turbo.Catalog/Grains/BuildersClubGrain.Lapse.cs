using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms;

namespace Turbo.Catalog.Grains;

/// <summary>
/// What happens to borrowed furni when the membership that lends it runs out: the rooms holding
/// it go off the navigator until the borrower renews or gives the furni back. The rows are the
/// truth, so this works from them rather than from the counts held in memory.
/// </summary>
internal sealed partial class BuildersClubGrain
{
    /// <summary>
    /// A renewal shows the rooms again at once rather than at the next sweep. Only the rooms
    /// this player has borrowed into are looked at, but every borrower of those rooms counts:
    /// one lapsed membership among them still hides the room.
    /// </summary>
    public Task OnSubscriptionChangedAsync(PlayerId playerId, CancellationToken ct) =>
        SweepAsync(playerId, ct);

    private Task SweepAsync(CancellationToken ct) => SweepAsync(null, ct);

    /// <summary>
    /// Brings every room holding borrowed furni to the visibility its borrowers' memberships
    /// call for, and shows again any room that is hidden and should not be. Only the rooms whose
    /// state actually has to change are touched, so a quiet hotel activates no room at all.
    /// </summary>
    private async Task SweepAsync(PlayerId? borrower, CancellationToken ct)
    {
        try
        {
            var (shouldHide, currentlyHidden) = await ReadVisibilityAsync(borrower, ct);

            foreach (var (roomId, hide) in shouldHide)
            {
                if (currentlyHidden.Contains(roomId) == hide)
                    continue;

                ApplyVisibility(roomId, hide, ct);
            }

            // A room that was hidden and holds no borrowed furni at all any more is in neither
            // list, so it would otherwise stay hidden for ever.
            foreach (var roomId in currentlyHidden)
            {
                if (!shouldHide.ContainsKey(roomId))
                    ApplyVisibility(roomId, false, ct);
            }
        }
        catch (Exception ex)
        {
            // One bad sweep must not stop the next; the rooms stay as they are.
            _logger.LogError(ex, "Failed to sweep lapsed Builders Club memberships");
        }
    }

    /// <summary>
    /// The room tells its owner and republishes its listing itself. Nothing here waits on it: a
    /// room that is slow to answer must not hold up the rest of the sweep, and the sweep runs
    /// again anyway.
    /// </summary>
    private void ApplyVisibility(RoomId roomId, bool hidden, CancellationToken ct) =>
        _grainFactory
            .GetRoomGrain(roomId)
            .SetHiddenByBuildersClubAsync(hidden, ct)
            .LogAndForget(
                _logger,
                $"set the Builders Club visibility of room {roomId} to {hidden}"
            );

    /// <summary>
    /// Which rooms ought to be hidden, and which are. Read in batched queries over the distinct
    /// borrowers of each room rather than one row per borrowed furni.
    /// </summary>
    /// <param name="borrower">
    /// When given, only the rooms this player has borrowed into are considered.
    /// </param>
    private async Task<(
        Dictionary<RoomId, bool> ShouldHide,
        HashSet<RoomId> CurrentlyHidden
    )> ReadVisibilityAsync(PlayerId? borrower, CancellationToken ct)
    {
        // Past its grace period, a membership no longer lends anything.
        var lapsedBefore = DateTime.UtcNow.AddDays(-_subscriptionConfig.GraceDays);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var furniture = dbCtx.BuildersClubFurnitures.AsNoTracking();

        if (borrower is { } playerId)
        {
            var theirRoomIds = await furniture
                .Where(x => x.PlacedByPlayerEntityId == playerId.Value)
                .Select(x => x.RoomEntityId)
                .Distinct()
                .ToListAsync(ct);

            furniture = furniture.Where(x => theirRoomIds.Contains(x.RoomEntityId));
        }

        var borrowers = await furniture
            .Select(x => new { x.RoomEntityId, x.PlacedByPlayerEntityId })
            .Distinct()
            .ToListAsync(ct);

        var borrowerIds = borrowers.Select(x => x.PlacedByPlayerEntityId).Distinct().ToList();

        var stillMembers = await dbCtx
            .PlayerSubscriptions.AsNoTracking()
            .Where(x =>
                x.SubscriptionType == SubscriptionType.BuildersClub
                && borrowerIds.Contains(x.PlayerEntityId)
                && x.ExpiresAt != null
                && x.ExpiresAt > lapsedBefore
            )
            .Select(x => x.PlayerEntityId)
            .ToListAsync(ct);

        var hidden = dbCtx.Rooms.AsNoTracking().Where(x => x.HiddenByBc);

        // A sweep for one player answers only for their rooms, so it must not offer to show
        // every other hidden room in the hotel as well.
        if (borrower is not null)
        {
            var consideredRoomIds = borrowers.Select(x => x.RoomEntityId).Distinct().ToList();

            hidden = hidden.Where(x => consideredRoomIds.Contains(x.Id));
        }

        var currentlyHidden = await hidden.Select(x => x.Id).ToListAsync(ct);

        var members = stillMembers.ToHashSet();
        var shouldHide = new Dictionary<RoomId, bool>();

        // One lapsed borrower is enough: the room holds furni the club is no longer lending.
        foreach (var entry in borrowers)
        {
            var roomId = RoomId.Parse(entry.RoomEntityId);

            shouldHide[roomId] =
                shouldHide.GetValueOrDefault(roomId)
                || !members.Contains(entry.PlacedByPlayerEntityId);
        }

        return (shouldHide, currentlyHidden.Select(RoomId.Parse).ToHashSet());
    }
}
