using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
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

    private const int VARIABLE_SPAWN = 0;
    private const int VARIABLE_VALUE = 1;
    private const int VARIABLE_COUNT = 2;

    // The bounds of the editor's offset inputs; the altitude is in hundredths of a tile.
    private const int OFFSET_MIN = -64;
    private const int OFFSET_MAX = 64;
    private const int OFFSET_ALTITUDE_MIN = -8000;
    private const int OFFSET_ALTITUDE_MAX = 8000;

    public override int WiredCode => (int)WiredActionType.PLACE_FURNI;

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
            WiredRules.VariableTarget(WiredVariableTargetType.User),
        ];

    // The furni to place, the custom target, and where the value variable is read.
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni, WiredSources.Furni, WiredSources.Furni];

    // The custom target, and where the value variable is read.
    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var sources = GetFurniSnapshot().Values.ToList();

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
        var map = _roomGrain.MapModule;
        var placed = false;

        foreach (var source in sources)
        {
            var definition = _roomGrain._definitionProvider.TryGetDefinition(source.DefinitionId);

            // A snapshot from before the type was recorded, or a furni type that is gone.
            if (definition is null || !Enum.IsDefined((Rotation)source.Rotation))
                continue;

            var x = source.X + dx;
            var y = source.Y + dy;

            if (!map.InBounds(x, y))
                continue;

            double z =
                altitude switch
                {
                    WiredPlaceAltitudeType.SourceAltitude => Altitude.FromInt(source.Z),
                    WiredPlaceAltitudeType.CustomAltitude => targetZ,
                    _ => map.GetTileHeight(map.ToIdx(x, y)),
                } + altitudeOffset;

            var item = await _roomGrain.FurniModule.PlaceTemporaryFloorItemAsync(
                actionCtx,
                definition,
                _ctx.RoomObject.OwnerId,
                x,
                y,
                Math.Clamp(z, 0, _roomGrain._roomConfig.MaxStackHeight),
                (Rotation)source.Rotation,
                ct
            );

            if (item is null)
                continue;

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

        return placed;
    }

    /// <summary>
    /// The furni variable every copy is given and its value, or null when the option is off or
    /// the variable is not one a furni can be given. The value is a literal or another
    /// variable's, read once for the whole group.
    /// </summary>
    private (IWiredVariable variable, int value)? GetSpawnVariable(IWiredExecutionContext ctx)
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

        return (variable, (int)Math.Clamp(value, int.MinValue, int.MaxValue));
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
