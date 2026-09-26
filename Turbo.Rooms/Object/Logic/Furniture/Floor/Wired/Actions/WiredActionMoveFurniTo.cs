using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Moves the furni of the first slot next to the furni of the second slot: N tiles away from
/// the target in the given cardinal direction. Params: direction (0, 2, 4 or 6) and tiles 1-5.
/// </summary>
[RoomObjectLogic("wf_act_move_furni_to")]
public class WiredActionMoveFurniTo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.MOVE_FURNI_TO;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<Rotation>(
                Rotation.North,
                Rotation.North,
                Rotation.East,
                Rotation.South,
                Rotation.West
            ),
            new WiredRangeParamRule(1, 5, 1),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.Furni, WiredSources.PickedFurni];

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var tiles = GetIntParamOrDefault(1, 1);
        var (dx, dy) = _roomGrain.MapModule.GetDirectionOffset(
            GetIntParamOrDefault(0, Rotation.North)
        );

        return MoveOntoTargetFurniAsync(ctx, dx * tiles, dy * tiles);
    }
}
