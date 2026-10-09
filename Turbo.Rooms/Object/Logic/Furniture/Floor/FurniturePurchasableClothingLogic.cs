using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// Clothing furni (furnidata <c>specialtype</c> 23, <c>figure_purchasable_set</c>; Sulake's
/// <c>furniture_purchasable_clothing</c> logic). Double-clicking it opens the client's
/// <c>PurchasableClothingConfirmationView</c> - "Using this furni will unlock the clothing in your
/// Avatar Editor…forever! Note: You can only use this once!" - and "Use &amp; Bind Clothing" sends
/// <c>CustomizeAvatarWithFurniMessageComposer</c> with the furni. Its owner then owns the figure
/// sets its <c>customparams</c> list, the furni is bound to them and used up, and the client, told
/// so by <c>FigureSetIdsMessage</c>, puts the clothes on.
/// </summary>
[RoomObjectLogic(LOGIC_NAME)]
public class FurniturePurchasableClothingLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx,
    IPlayerClothingService clothing
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public const string LOGIC_NAME = "purchasable_clothing";

    private readonly IPlayerClothingService _clothing = clothing;

    private bool _binding;

    /// <summary>The figure set ids a clothing furni's <c>customparams</c> list, or none.</summary>
    public static IReadOnlyList<int> SetIdsOf(FurnitureDefinitionSnapshot definition)
    {
        var setIds = new List<int>();

        if (definition.FurniCategory != FurnitureCategory.FigurePurchasableSet)
            return setIds;

        foreach (
            var part in (definition.CustomParams ?? string.Empty).Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
            )
        )
        {
            if (int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var setId))
                setIds.Add(setId);
        }

        return setIds;
    }

    // The client opens its dialog rather than using the furni.
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not BindClothingInteraction)
            return false;

        if (!IsItemOwner(ctx))
            return Reject(ctx, interaction, "not the owner");

        var setIds = SetIdsOf(_ctx.Definition);

        if (setIds.Count == 0)
            return Reject(ctx, interaction, "definition lists no figure sets");

        if (_binding)
            return Reject(ctx, interaction, "already being bound");

        _binding = true;

        try
        {
            await _clothing.BindFurnitureAsync(ctx.PlayerId, _ctx.Definition.Id, setIds, ct);

            // The sets are the owner's now; the furni must not survive to be bound twice.
            return await ActionModule.DeleteItemByIdAsync(ctx, _ctx.ObjectId, ct);
        }
        finally
        {
            _binding = false;
        }
    }
}
