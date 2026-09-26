using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A mannequin holds an outfit (clothing parts only) in map data under the keys the client reads.
/// The owner dresses and names it; anyone of the same gender wears it with a double-click.
/// </summary>
[RoomObjectLogic("mannequin")]
public class FurnitureMannequinLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    protected override StuffDataType _stuffDataType => StuffDataType.MapKey;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Everybody;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (StuffData is not IMapStuffData map)
            return;

        var figure = map.ValueOf(MannequinData.FIGURE);
        var gender = map.ValueOf(MannequinData.GENDER);

        if (figure.Length == 0 || gender.Length == 0)
            return;

        var wearer = await _roomGrain
            ._grainFactory.GetPlayerGrain(ctx.PlayerId)
            .GetSummaryAsync(ct);

        if (
            !string.Equals(
                wearer.Gender.ToLegacyString(),
                gender,
                StringComparison.OrdinalIgnoreCase
            )
        )
            return;

        _roomGrain.AvatarModule.ChangePlayerFigure(
            ctx.PlayerId,
            MannequinData.Dress(wearer.Figure, figure),
            wearer.Gender
        );
    }

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not (SetMannequinFigureInteraction or SetMannequinNameInteraction))
            return false;

        if (!await IsOwnerAsync(ctx))
            return Reject(ctx, interaction, "not the owner");

        if (interaction is SetMannequinNameInteraction rename)
        {
            var name = rename.Name.Trim();

            if (name.Length == 0 || name.Length > _roomGrain._roomConfig.MannequinNameMaxLength)
                return Reject(ctx, interaction, "name length");

            return await SetMapDataAsync(
                new Dictionary<string, string> { [MannequinData.OUTFIT_NAME] = name }
            );
        }

        var owner = await _roomGrain._grainFactory.GetPlayerGrain(ctx.PlayerId).GetSummaryAsync(ct);

        return await SetMapDataAsync(
            new Dictionary<string, string>
            {
                [MannequinData.GENDER] = owner.Gender.ToLegacyString(),
                [MannequinData.FIGURE] = MannequinData.ExtractClothing(owner.Figure),
            }
        );
    }
}
