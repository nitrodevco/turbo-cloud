using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Teleports the selected users onto one of the picked furni, chosen at random. The user gets the
/// teleport effect when the box fires, is set on the target later by a plain status update, never
/// a wired movement, and loses the effect after that (official client capture 2026-10-16,
/// walk-on + Teleport To, 2 of 2 runs each way). Param 0 is the editor's "fast teleportation":
/// without it effect 4, the user walks on, lands +2.0 s, effect off +3.5 s; with it effect 235,
/// the walk ends on the tile being stepped onto (standing at the next cycle), lands +1.0 s,
/// effect off +1.5 s. A furni nobody can stand on puts the user beside it (<see cref="TryGetLandingTile"/>).
/// </summary>
[RoomObjectLogic("wf_act_teleport_to")]
public class WiredActionTeleportTo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.TELEPORT;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var items = GetFloorItems(selection);
        var players = GetAvatars(selection);

        if (items.Count == 0 || players.Count == 0)
            return false;

        var fast = GetIntParamOrDefault(0, false);
        var moved = false;

        foreach (var player in players)
        {
            var target = items[Random.Shared.Next(items.Count)];

            if (!TryGetLandingTile(player, target, out var tileIdx))
                continue;

            moved |= await BeginTeleportAsync(player, tileIdx, fast, ct);
        }

        return moved;
    }

    /// <summary>
    /// Where the user lands for a furni: on its tile when it can be stood on, otherwise on the
    /// first free tile beside it, starting with the one in front of it (its rotation) and going
    /// round clockwise, diagonals included, whatever the height (official client capture
    /// 2026-10-18 onto an antenna: rotation 0, 2, 4, 6 landed north, east, south, west of it, 2/2
    /// each; the front tile taken, the next clockwise, 2/2 each for 6 -> 7, 6+7 -> 0, 0 -> 1).
    /// Users on a tile are no obstacle to a teleport; nowhere to stand means no teleport.
    /// </summary>
    private bool TryGetLandingTile(IRoomAvatar avatar, IRoomFloorItem furni, out int tileIdx)
    {
        tileIdx = MapModule.ToIdx(furni.X, furni.Y);

        if (!MapModule.InBounds(tileIdx))
            return false;

        if (MapModule.CanAvatarWalk(avatar, tileIdx, true, ignoreAvatars: true))
            return true;

        var front = furni.Rotation == Rotation.None ? 0 : (int)furni.Rotation;

        for (var turn = 0; turn < 8; turn++)
        {
            if (
                MapModule.TryGetTileInFront(
                    tileIdx,
                    (Rotation)((front + turn) % 8),
                    out var besideIdx
                ) && MapModule.CanAvatarWalk(avatar, besideIdx, true, ignoreAvatars: true)
            )
            {
                tileIdx = besideIdx;

                return true;
            }
        }

        return false;
    }

    private async Task<bool> BeginTeleportAsync(
        IRoomAvatar avatar,
        int tileIdx,
        bool fast,
        CancellationToken ct
    )
    {
        if (MapModule.ToIdx(avatar.X, avatar.Y) == tileIdx)
            return false;

        var config = WiredConfig;
        var effectId = fast ? config.TeleportFastEffectId : config.TeleportEffectId;
        var worn = avatar.EffectId;

        if (effectId > 0)
            await AvatarModule.SetAvatarEffectAsync(avatar.ObjectId, effectId, ct);

        var effectMs = fast ? config.TeleportFastEffectMs : config.TeleportEffectMs;

        // The step under way ends where it goes; the room sees the user stand at the next cycle.
        if (fast)
        {
            await AvatarModule.StopWalkingAsync(avatar, ct);

            WiredSystem.RunAfter(
                config.TeleportFastDelayMs,
                token => ArriveAsync(avatar, tileIdx, token)
            );
        }
        else
        {
            // The room is told of the user where they stand one avatar cycle before they land
            // (a sitter still sitting), and they land on the first cycle more than
            // TeleportDelayMs less a cycle after the fire: 1.5 to 2.0 s, two seconds for a fire on
            // a cycle (walk-on); the effect ends as long after landing as it does then (official
            // client capture 2026-10-16 to 2026-10-24, every Teleport To recorded: the extra
            // UserUpdate +1.0 to +1.5 s, landing 0.5 s and AvatarEffect 0 2.0 s after it; wired
            // matrix action.8.B3/B4). Work done between two cycles goes out on the second.
            var tickMs = RoomConfig.AvatarTickMs;
            var now = WiredSystem.DeferredNowMs;
            var landsAt = AvatarCycleAfter(now + config.TeleportDelayMs - tickMs);

            WiredSystem.RunAfter(
                (int)(landsAt - 2 * tickMs - now),
                token =>
                {
                    if (IsInRoom(avatar))
                        avatar.MarkDirty();

                    return Task.CompletedTask;
                }
            );
            WiredSystem.RunAfter(
                (int)(landsAt - tickMs - now),
                token => ArriveAsync(avatar, tileIdx, token)
            );

            effectMs = (int)(landsAt + config.TeleportEffectMs - config.TeleportDelayMs - now);
        }

        // A user still frozen when the teleport's effect ends gets their freeze effect back; anyone
        // else none (official client capture 2026-10-24, Freeze with "cancel on teleport" off then
        // Teleport To: AvatarEffect 4 at the fire, 218 again 3.4 s later, still frozen; with it on,
        // thawed on landing and AvatarEffect 0 at the same point, 1/1 each).
        if (effectId > 0)
            WiredSystem.RunAfter(
                effectMs,
                token =>
                    IsInRoom(avatar) && avatar.EffectId == effectId
                        ? AvatarModule.SetAvatarEffectAsync(
                            avatar.ObjectId,
                            avatar.IsFrozen ? worn : 0,
                            token
                        )
                        : Task.FromResult(false)
            );

        return true;
    }

    /// <summary>
    /// The user lands on the tile chosen when the box fired: a teleport goes past users on the
    /// tile, but not onto a tile closed or covered by a furni that cannot be stood on by then.
    /// The room hears of it as a status update.
    /// </summary>
    private async Task ArriveAsync(IRoomAvatar avatar, int tileIdx, CancellationToken ct)
    {
        if (
            !IsInRoom(avatar)
            || !MapModule.CanAvatarWalk(avatar, tileIdx, true, ignoreAvatars: true)
        )
            return;

        if (avatar.IsFrozen && avatar.ThawsOnTeleport)
            avatar.SetFrozen(false);

        await AvatarModule.RelocateAvatarAsync(avatar, tileIdx, ct);
    }
}
