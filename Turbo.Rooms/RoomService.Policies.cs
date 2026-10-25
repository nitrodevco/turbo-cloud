using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms;

internal sealed partial class RoomService
{
    /// <summary>
    /// Asks the placement policies (<see cref="IPlacementPolicy"/>) before the room is asked. A
    /// refused furni stays where it was and the player is shown the policy's reason, or the
    /// hotel's "you cannot place this item here" without one; a refused floor plan is answered
    /// with the editor's own error pop-up, its errors the reason, or not at all without one (as
    /// the room answers a save it will not take).
    /// </summary>
    private async Task<bool> PassesPlacementPolicyAsync(
        PlacementRequest request,
        CancellationToken ct
    )
    {
        var decision = await _policies.DecidePlacementAsync(request, ct).ConfigureAwait(false);

        if (decision.Allowed)
            return true;

        if (request.Kind == PlacementKind.FloorPlan)
        {
            if (!string.IsNullOrEmpty(decision.Reason))
                await _grainFactory
                    .SendComposerToPlayerAsync(
                        request.PlayerId,
                        new NotificationDialogMessageComposer
                        {
                            NotificationType = RoomGrain.FLOOR_PLAN_ERROR_NOTIFICATION,
                            Parameters = ImmutableDictionary<string, string>.Empty.Add(
                                "errors",
                                decision.Reason
                            ),
                        },
                        ct
                    )
                    .ConfigureAwait(false);

            return false;
        }

        if (string.IsNullOrEmpty(decision.Reason))
            await SendPlacementErrorAsync(
                    request.PlayerId,
                    FurniturePlacementResultType.Refused,
                    ct
                )
                .ConfigureAwait(false);
        else
            await _grainFactory
                .SendComposerToPlayerAsync(
                    request.PlayerId,
                    PolicyRefusals.Notification(PolicyRefusals.PLACEMENT, decision.Reason),
                    ct
                )
                .ConfigureAwait(false);

        return false;
    }

    /// <summary>The furni a builders club offer places; null when the hotel does not know it.</summary>
    private FurnitureDefinitionSnapshot? BuildersClubDefinition(int offerId) =>
        catalog is not null
        && catalog
            .GetCatalogSnapshot(CatalogType.BuildersClub)
            .OffersById.TryGetValue(offerId, out var offer)
        && offer.Products.FirstOrDefault() is { } product
            ? definitions?.TryGetDefinition(product.FurniDefinitionId)
            : null;
}
