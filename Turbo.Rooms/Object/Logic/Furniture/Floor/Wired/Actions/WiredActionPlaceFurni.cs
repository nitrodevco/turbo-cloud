using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// "Place Temporary Furni": puts copies of the picked furni in the room. The copies are
/// temporary furni (<c>RoomFurniModule.PlaceTemporaryFloorItemAsync</c>): nobody owns them, they
/// are gone when the room unloads, and <c>wf_act_remove_furni</c> takes them away sooner. What
/// is copied is the box's snapshot of the picked furni, taken when it was saved (type, tile,
/// altitude, rotation, state), so the originals need not stay in the room.
///
/// Int params, as the client's editor writes them: whether the custom target is a user
/// (otherwise the second furni slot), target location, target altitude, offset x, offset y
/// (tiles), offset altitude (hundredths), spawn with a variable, then that variable's value as
/// a value-or-variable input (switch, one int, target). Variable ids are positional: the furni
/// variable every copy is given, and the variable its value is read from.
///
/// A group keeps its layout: at a custom location the first furni of the snapshot lands on the
/// target and the others keep their places relative to it, as with
/// <see cref="WiredActionMoveAsGroup"/>.
/// </summary>
[RoomObjectLogic("wf_act_place_furni")]
public class WiredActionPlaceFurni(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_TARGET_IS_USER = 0;
    private const int PARAM_LOCATION = 1;
    private const int PARAM_ALTITUDE = 2;
    private const int PARAM_OFFSET_X = 3;
    private const int PARAM_OFFSET_Y = 4;
    private const int PARAM_OFFSET_ALTITUDE = 5;
    private const int PARAM_SPAWN_WITH_VARIABLE = 6;
    private const int PARAM_VALUE_IS_VARIABLE = 7;
    private const int PARAM_VALUE = 8;
    private const int PARAM_VALUE_TARGET = 9;

    private const int SLOT_TARGET_FURNI = 1;

    // The dialog Habbo sends when a copy cannot be placed, as recorded.
    private const string PLACEMENT_FAILURE_TYPE = "wired.error";
    private const string PLACEMENT_FAILURE_IMAGE = "${image.library.url}album1584/WIRD2.png";
    private const string PLACEMENT_FAILURE_MESSAGE = "${room.error.wired.placement_failure}";

    private const int VARIABLE_SPAWN = 0;
    private const int VARIABLE_VALUE = 1;
    private const int VARIABLE_COUNT = 2;

    // The bounds of the editor's offset inputs; the altitude is in hundredths of a tile.
    private const int OFFSET_MIN = -64;
    private const int OFFSET_MAX = 64;
    private const int OFFSET_ALTITUDE_MIN = -8000;
    private const int OFFSET_ALTITUDE_MAX = 8000;

    public override int WiredCode => (int)WiredActionType.PLACE_FURNI;

    protected override bool AllowsWallFurni => true;

    protected override bool KeepsFurniSnapshot => true;

    protected override bool HasPositionalVariableIds => true;

    public override int GetMaxVariableIds() => VARIABLE_COUNT;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredEnumParamRule<WiredPlaceLocationType>(WiredPlaceLocationType.SourceLocation),
            new WiredEnumParamRule<WiredPlaceAltitudeType>(
                WiredPlaceAltitudeType.OnTopOfTargetLocation
            ),
            new WiredRangeParamRule(OFFSET_MIN, OFFSET_MAX, 0),
            new WiredRangeParamRule(OFFSET_MIN, OFFSET_MAX, 0),
            new WiredRangeParamRule(OFFSET_ALTITUDE_MIN, OFFSET_ALTITUDE_MAX, 0),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            WiredRules.AnyInt(),
            // A fresh box reads the value from a context variable (official client capture:
            // defaultIntParams [0 x9, -20]).
            WiredRules.VariableTarget(WiredVariableTargetType.Context),
        ];

    // The furni to place (the snapshot by default, or furni as they stand now), the custom
    // target, and where the value variable is read (official client capture 2026-10-14:
    // [0,100,101,110,200,201] / [0,100,101,200,201] x2, defaults 110, 0, 200).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [WiredFurniSourceType.SnapshotItems, .. WiredSources.FurniOrSecondary],
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.TriggeredItem),
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.SelectorItems),
        ];

    // The custom target, and where the value variable is read (recorded defaults 0, 200).
    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users.DefaultingTo(WiredPlayerSourceType.SelectorUsers)];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    /// <summary>
    /// Refuses a save that picks furni to copy that may not be copied, as Habbo does: with
    /// <c>wiredfurni.error.furni_can_not_be_cloned</c> and the old configuration kept (official
    /// client capture, WIN63-202610131318: a non-tradable chair from the inventory refused, a
    /// lamp and a building block borrowed from the Builders Club copied). Only the furni to copy
    /// is checked, not the custom target.
    /// </summary>
    public override Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        foreach (var proposedId in update.StuffIds)
        {
            if (
                FurniModule.TryGetItem(
                    WiredFurniIds.FromClient(FurniModule, proposedId),
                    out var item
                ) && !CanBeCopied(item)
            )
                throw new WiredSaveRefusedException(WiredSaveErrors.FURNI_CAN_NOT_BE_CLONED);
        }

        return base.ApplyWiredUpdateAsync(ctx, update, ct);
    }

    /// <summary>
    /// Builders Club furni (<see cref="IRoomItem.IsBuildersClub"/>) "that do not have advanced
    /// functionality" (the error's text). Both copies Habbo made were of borrowed furni and the
    /// refused one was the player's own; that the borrowing, not the type, decides is inferred
    /// from those three and the text. What counts as advanced is not recorded either: here it is
    /// every furni that turns into something of value, which no hotel may let wired copy.
    /// </summary>
    private bool CanBeCopied(IRoomItem item) =>
        (item.IsBuildersClub || !WiredConfig.PlaceFurniBuildersClubOnly)
        && item.Logic
            is not (
                FurnitureExchangeLogic
                or FurniturePresentLogic
                or FurnitureCrackableLogic
                or FurnitureEffectBoxLogic
                or FurniturePurchasableClothingLogic
                or FurniturePetPackageLogic
                or FurniturePetProductLogic
            );

    /// <summary>
    /// What is copied: the box's snapshot of the picked furni for "Use the snapshot", otherwise
    /// the furni of slot 0 as they stand now (picked, secondary picks, triggering, selector or
    /// signal furni).
    /// </summary>
    private List<(int Id, WiredFurniSnapshotEntry Entry)> FurniToPlace(IWiredContext ctx)
    {
        if (GetFurniSources()[0].Contains(WiredFurniSourceType.SnapshotItems))
            return [.. GetFurniSnapshot().Select(x => (x.Key, x.Value))];

        return
        [
            .. GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 0))
                .Select(item => (item.ObjectId.Value, EntryOf(item))),
        ];
    }

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var toPlace = FurniToPlace(ctx);
        var sources = toPlace.Select(x => x.Entry).ToList();

        if (sources.Count == 0)
            return false;

        var location = GetIntParamOrDefault(PARAM_LOCATION, WiredPlaceLocationType.SourceLocation);
        var altitude = GetIntParamOrDefault(
            PARAM_ALTITUDE,
            WiredPlaceAltitudeType.OnTopOfTargetLocation
        );
        var (targetX, targetY, targetZ) = (0, 0, Altitude.Zero);

        if (
            (
                location == WiredPlaceLocationType.CustomLocation
                || altitude == WiredPlaceAltitudeType.CustomAltitude
            ) && !TryGetCustomTarget(ctx, out targetX, out targetY, out targetZ)
        )
            return false;

        var dx = GetIntParamOrDefault(PARAM_OFFSET_X, 0);
        var dy = GetIntParamOrDefault(PARAM_OFFSET_Y, 0);

        if (location == WiredPlaceLocationType.CustomLocation)
        {
            dx += targetX - sources[0].X;
            dy += targetY - sources[0].Y;
        }

        var altitudeOffset = Altitude.FromInt(GetIntParamOrDefault(PARAM_OFFSET_ALTITUDE, 0));
        var spawn = GetSpawnVariable(ctx);
        var actionCtx = ctx.AsActionContext();
        var map = MapModule;
        var placed = false;
        var failed = false;

        foreach (var (sourceId, source) in toPlace)
        {
            // A box saved before the snapshot kept the furni's type copies the picked furni as it
            // is now, while it is still in the room.
            var definition =
                source.DefinitionId > 0
                    ? _roomGrain._definitionProvider.TryGetDefinition(source.DefinitionId)
                : TryGetFloorItem(sourceId, out var picked) ? picked.Definition
                : null;

            // A snapshot from before the type was recorded, or a furni type that is gone.
            if (definition is null || !Enum.IsDefined((Rotation)source.Rotation))
                continue;

            var x = source.X + dx;
            var y = source.Y + dy;

            // Off the map is a copy that cannot be placed like any other.
            if (!map.InBounds(x, y))
            {
                failed = true;

                continue;
            }

            double z =
                altitude switch
                {
                    WiredPlaceAltitudeType.SourceAltitude => Altitude.FromInt(source.Z),
                    WiredPlaceAltitudeType.CustomAltitude => targetZ,
                    _ => map.GetTileHeight(map.ToIdx(x, y)),
                } + altitudeOffset;

            var item = await FurniModule.PlaceTemporaryFloorItemAsync(
                actionCtx,
                definition,
                _ctx.RoomObject.OwnerId,
                x,
                y,
                Math.Clamp(z, 0, RoomConfig.MaxStackHeight),
                (Rotation)source.Rotation,
                ct
            );

            if (item is null)
            {
                failed = true;

                continue;
            }

            placed = true;

            if (source.State != 0)
                await item.Logic.SetStateAsync(source.State);

            if (spawn is var (variable, value))
                await variable.GiveValueAsync(
                    new WiredVariableKey(
                        variable.GetVarSnapshot().VariableId,
                        WiredVariableTargetType.Furni,
                        item.ObjectId
                    ),
                    new WiredVariableValue(value),
                    true
                );
        }

        if (failed)
            await NotifyPlacementFailureAsync(ctx, ct);

        return placed;
    }

    /// <summary>
    /// Tells the triggering user that a copy could not be placed, as Habbo does: a "wired.error"
    /// notification dialog (official client capture, WIN63-202610131318, owner's room): once per
    /// run however many copies failed, and to the same user at most once per
    /// <see cref="WiredConfig.PlacementFailureNotificationCooldownMs"/> (runs 2.6, 3.6 and 4.4 s
    /// after the last dialog got none, 6.6 s and later got one). Inference: the triggering user
    /// is the one told (the room owner triggered every recorded run), and a run started by no
    /// user tells nobody.
    /// </summary>
    private async Task NotifyPlacementFailureAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var now = _roomGrain.NowMs();
        var cooldown = WiredConfig.PlacementFailureNotificationCooldownMs;
        var notified = State.WiredPlacementFailureNotifiedMs;

        // Only who is still waiting is kept, so the list does not grow with every visitor.
        foreach (
            var playerId in notified
                .Where(x => now - x.Value >= cooldown)
                .Select(x => x.Key)
                .ToList()
        )
            notified.Remove(playerId);

        foreach (var avatarId in ctx.Selected.SelectedAvatarIds)
        {
            if (
                !AvatarModule.TryGetAvatar(avatarId, out var avatar)
                || avatar is not IRoomPlayer player
            )
                continue;

            if (notified.TryGetValue(player.PlayerId, out var last) && now - last < cooldown)
                continue;

            notified[player.PlayerId] = now;

            await GrainFactory.SendComposerToPlayerAsync(
                player.PlayerId,
                new NotificationDialogMessageComposer
                {
                    NotificationType = PLACEMENT_FAILURE_TYPE,
                    Parameters = ImmutableDictionary<string, string>
                        .Empty.Add("image", PLACEMENT_FAILURE_IMAGE)
                        .Add("message", PLACEMENT_FAILURE_MESSAGE),
                },
                ct
            );
        }
    }

    /// <summary>
    /// The furni variable every copy is given and its value, or null when the option is off or
    /// the variable is not one a furni can be given. The value is a literal or another
    /// variable's, read once for the whole group.
    /// </summary>
    private (IWiredVariable variable, long value)? GetSpawnVariable(IWiredExecutionContext ctx)
    {
        if (
            !GetIntParamOrDefault(PARAM_SPAWN_WITH_VARIABLE, false)
            || GetVariable(VARIABLE_SPAWN) is not { } variable
        )
            return null;

        var snapshot = variable.GetVarSnapshot();

        if (
            snapshot.TargetType != WiredVariableTargetType.Furni
            || !snapshot.Flags.Has(WiredVariableFlags.CanCreateAndDelete)
        )
            return null;

        long value = GetIntParamOrDefault(PARAM_VALUE, 0);

        if (
            GetIntParamOrDefault(PARAM_VALUE_IS_VARIABLE, false)
            && !TryReadVariableOperand(
                VARIABLE_VALUE,
                PARAM_VALUE_TARGET,
                ctx.GetSelection(this),
                out value
            )
        )
            value = 0;

        return (variable, value);
    }

    private bool TryGetCustomTarget(
        IWiredExecutionContext ctx,
        out int x,
        out int y,
        out Altitude z
    )
    {
        (x, y, z) = (0, 0, Altitude.Zero);

        if (GetIntParamOrDefault(PARAM_TARGET_IS_USER, false))
        {
            var players = GetPlayers(ctx.GetSelection(this));

            if (players.Count == 0)
                return false;

            (x, y, z) = (players[0].X, players[0].Y, players[0].Z);

            return true;
        }

        var targets = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_TARGET_FURNI));

        if (targets.Count == 0)
            return false;

        (x, y, z) = (targets[0].X, targets[0].Y, targets[0].Z);

        return true;
    }
}
