using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// The named bots appear on one of the picked furni, each on a furni drawn for it, whatever
/// stands on its tile (<see cref="RoomBotModule.TeleportToItemAsync"/>). Each bot gets the
/// teleport effect when the box fires, is set on the furni ~1.5 s later and loses the effect
/// ~3.1 s after the fire (official client capture 2026-10-24, action.21.R3 3/3). A furni picked
/// up by then takes no bot; one moved takes it to where it stands now (action.21.R4/R5).
/// </summary>
[RoomObjectLogic("wf_act_bot_teleport")]
public class WiredActionBotTeleport(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredBotActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int TELEPORT_EFFECT_ID = 4;
    private const int ARRIVE_AFTER_MS = 1500;
    private const int EFFECT_OFF_AFTER_MS = 3100;

    public override int WiredCode => (int)WiredActionType.BOT_TELEPORT;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var (botName, _) = SplitParam();

        if (ResolveBots(ctx, SLOT_BOTS, botName) is not { Count: > 0 } bots)
            return false;

        var items = GetFloorItems(ctx.GetSelection(this));

        if (items.Count == 0)
            return false;

        foreach (var bot in bots)
        {
            var itemId = items[Random.Shared.Next(items.Count)].ObjectId;

            await AvatarModule.SetAvatarEffectAsync(bot.ObjectId, TELEPORT_EFFECT_ID, ct);

            WiredSystem.RunAfter(
                ARRIVE_AFTER_MS,
                token =>
                    IsInRoom(bot)
                    && FurniModule.TryGetItem(itemId, out var item)
                    && item is IRoomFloorItem furni
                        ? BotModule.TeleportToItemAsync(bot, furni, token)
                        : Task.CompletedTask
            );
            WiredSystem.RunAfter(
                EFFECT_OFF_AFTER_MS,
                token =>
                    IsInRoom(bot) && bot.EffectId == TELEPORT_EFFECT_ID
                        ? AvatarModule.SetAvatarEffectAsync(bot.ObjectId, 0, token)
                        : Task.CompletedTask
            );
        }

        return true;
    }

    private bool IsInRoom(IRoomBot bot) =>
        AvatarModule.TryGetAvatar(bot.ObjectId, out var current) && current == bot;
}
