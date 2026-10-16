using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
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
/// effect off +1.5 s.
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
        var map = MapModule;
        var moved = false;

        foreach (var player in players)
        {
            var target = items[Random.Shared.Next(items.Count)];
            var tileIdx = map.ToIdx(target.X, target.Y);

            moved |= await BeginTeleportAsync(player, tileIdx, fast, ct);
        }

        return moved;
    }

    private async Task<bool> BeginTeleportAsync(
        IRoomAvatar avatar,
        int tileIdx,
        bool fast,
        CancellationToken ct
    )
    {
        if (
            !MapModule.InBounds(tileIdx)
            || MapModule.ToIdx(avatar.X, avatar.Y) == tileIdx
            || !MapModule.CanAvatarWalk(avatar, tileIdx, true, ignoreAvatars: true)
        )
            return false;

        var config = _roomGrain._wiredConfig;
        var effectId = fast ? config.TeleportFastEffectId : config.TeleportEffectId;

        if (effectId > 0)
            await AvatarModule.SetAvatarEffectAsync(avatar.ObjectId, effectId, ct);

        // The step under way ends where it goes; the room sees the user stand at the next cycle.
        if (fast)
            await AvatarModule.StopWalkingAsync(avatar, ct);

        WiredSystem.RunAfter(
            fast ? config.TeleportFastDelayMs : config.TeleportDelayMs,
            token => ArriveAsync(avatar, tileIdx, token)
        );

        if (effectId > 0)
            WiredSystem.RunAfter(
                fast ? config.TeleportFastEffectMs : config.TeleportEffectMs,
                token =>
                    IsInRoom(avatar) && avatar.EffectId == effectId
                        ? AvatarModule.SetAvatarEffectAsync(avatar.ObjectId, 0, token)
                        : Task.FromResult(false)
            );

        return true;
    }

    /// <summary>
    /// The user lands: a teleport goes past users on the tile, but not onto a closed tile or a
    /// furni that cannot be stood on by then. The room hears of it as a status update.
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

    private bool IsInRoom(IRoomAvatar avatar) =>
        AvatarModule.TryGetAvatar(avatar.ObjectId, out var current) && current == avatar;
}
