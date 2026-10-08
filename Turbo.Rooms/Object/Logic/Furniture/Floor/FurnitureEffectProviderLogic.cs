using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A furni that puts an avatar effect on whoever uses it, as a vending machine hands out a drink
/// (Sulake's <c>AvatarEffectProviderFurniture</c>: the Habbo Gun Vender, sword and gun racks).
/// The effect is its definition's <c>customparams</c>; it is the hotel's, not one the player
/// owns, so it is not added to their effects. How it serves is
/// <see cref="FurnitureServingLogic"/>'s.
/// </summary>
[RoomObjectLogic("effect_provider")]
public class FurnitureEffectProviderLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureServingLogic(stuffDataFactory, ctx)
{
    private readonly int _effectId = AvatarEffectFurni.EffectIdOf(ctx.Definition) ?? 0;

    protected override bool CanServe => _effectId > 0;

    protected override bool Animates => _ctx.Definition.TotalStates > 1;

    protected override Task HandOverAsync(IRoomAvatar avatar, CancellationToken ct) =>
        AvatarModule.SetAvatarEffectAsync(avatar.ObjectId, _effectId, ct);
}
