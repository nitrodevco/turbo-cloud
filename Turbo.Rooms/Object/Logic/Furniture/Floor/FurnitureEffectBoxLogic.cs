using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// An effect box (Sulake's <c>EffectBoxFurniture</c>, the <c>fxbox_*</c> furni). Its owner opens
/// it once, after the client's confirmation: "This awesome effect box can only be used once ...
/// After clicking 'OK' you will wear the effect until you exit the room. You can always
/// reactivate the effect going in the 'Change Look' and selecting the effects tab"
/// (<c>effectbox.header.description</c>). So the box is gone, the effect (its definition's
/// <c>customparams</c>) is the owner's for good, and they wear it at once. A box whose effect
/// the owner already has for good, or that the hotel cannot give, is left as it is.
/// </summary>
[RoomObjectLogic("effect_box")]
public class FurnitureEffectBoxLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private readonly int _effectId = AvatarEffectFurni.EffectIdOf(ctx.Definition) ?? 0;

    private bool _opening;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (
            _opening
            || _effectId <= 0
            || ctx.Origin != ActionOrigin.Player
            || ctx.PlayerId != _ctx.RoomObject.OwnerId
        )
            return;

        _opening = true;

        try
        {
            var effects = _roomGrain._grainFactory.GetPlayerEffectGrain(ctx.PlayerId);
            var result = await effects.GiveEffectAsync(_effectId, 0, 0, true, ct);

            if (result != EffectGrantResult.Granted)
            {
                _roomGrain._logger.LogInformation(
                    "Effect box {ItemId} ({Definition}) in room {RoomId} was not opened for player {PlayerId}: {Result}",
                    _ctx.ObjectId,
                    _ctx.Definition.Name,
                    _ctx.RoomId,
                    ctx.PlayerId,
                    result
                );

                return;
            }

            await ActionModule.DeleteItemByIdAsync(ctx, _ctx.ObjectId, ct);

            // Wearing goes through the presence and back to this room, so it is told, not awaited.
            effects
                .SelectEffectAsync(_effectId, ct)
                .LogAndForget(_roomGrain._logger, "wear the effect of an opened effect box");
        }
        finally
        {
            _opening = false;
        }
    }
}
