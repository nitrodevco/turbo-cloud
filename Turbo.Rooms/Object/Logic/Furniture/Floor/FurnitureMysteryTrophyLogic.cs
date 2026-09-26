using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A mystery trophy waits for its owner to engrave it; the inscription turns it into an ordinary
/// trophy (same item, trophy-shaped data). Engraving happens once.
/// </summary>
[RoomObjectLogic("mystery_trophy")]
public class FurnitureMysteryTrophyLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not EngraveTrophyInteraction engrave)
            return false;

        if (!await IsItemOrRoomOwnerAsync(ctx))
            return Reject(ctx, interaction, "not the owner");

        if (GetLegacyString().Contains(TrophyData.SEPARATOR))
            return Reject(ctx, interaction, "already engraved");

        var inscription = engrave.Inscription.Trim();

        if (inscription.Length > _roomGrain._roomConfig.TrophyInscriptionMaxLength)
            return Reject(ctx, interaction, "inscription length");

        var owner = await _roomGrain._grainFactory.GetPlayerGrain(ctx.PlayerId).GetSummaryAsync(ct);
        var date = DateTime.UtcNow.ToString(TrophyData.DATE_FORMAT, CultureInfo.InvariantCulture);

        await SetLegacyDataAsync(TrophyData.Compose(owner.Name, date, inscription));

        return true;
    }
}
