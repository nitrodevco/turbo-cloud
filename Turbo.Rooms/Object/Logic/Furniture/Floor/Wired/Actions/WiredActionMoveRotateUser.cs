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
/// Slides and turns the selected users. Params: move direction (-1 none, 0-7) and rotation
/// (-1 none, 0-7 face that way, 9 turn clockwise, 10 turn counter-clockwise).
/// </summary>
[RoomObjectLogic("wf_act_move_rotate_user")]
public class WiredActionMoveRotateUser(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int NONE = -1;
    private const int ROTATE_CLOCKWISE = 9;
    private const int ROTATE_COUNTER_CLOCKWISE = 10;

    public override int WiredCode => (int)WiredActionType.MOVE_USER;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(-1, 7, -1), new WiredRangeParamRule(-1, 10, -1)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var moveDirection = GetIntParamOrDefault(0, NONE);
        var rotation = GetIntParamOrDefault(1, NONE);
        var map = _roomGrain.MapModule;
        var affected = false;

        foreach (var player in GetAvatars(ctx.GetSelection(this)))
        {
            if (moveDirection != NONE)
            {
                var idx = map.ToIdx(player.X, player.Y);

                if (map.TryGetTileInFront(idx, (Rotation)moveDirection, out var nextIdx))
                    affected |= await ctx.ProcessUserMovementAsync(
                        player,
                        nextIdx,
                        SlideAvatarMoveType.Slide
                    );
            }

            if (rotation == NONE)
                continue;

            var body = rotation switch
            {
                ROTATE_CLOCKWISE => player.Rotation.Rotate(2),
                ROTATE_COUNTER_CLOCKWISE => player.Rotation.Rotate(-2),
                _ => (Rotation)rotation,
            };

            await ctx.ProcessUserDirectionAsync(player, body, body);

            affected = true;
        }

        return affected;
    }
}
