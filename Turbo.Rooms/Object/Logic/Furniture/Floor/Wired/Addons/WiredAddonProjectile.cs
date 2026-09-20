using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// "Projectile" (the furni is <c>wf_xtra_rotate_to_dir</c>): dresses up the moves the stack
/// makes with the furni picked as projectiles, and only those. It turns a projectile to face
/// the way it flies, bends its trajectory, and lets the animation fly past its target.
///
/// Int params, as the client's editor writes them (nineteen, all declared so a save comes back
/// as it was made): rotate in moving direction, directional system, scale animation time with
/// distance, time per tile as value-or-variable (switch, int, target), distance by x, by y, by
/// height, speed increase, rotation offset (eighth turns), internal variables mask, change the
/// shooter's direction, bunny hop, animation distance mode, its tiles as value-or-variable
/// (switch, int, target), curve strength (zero is a straight trajectory). Variable ids are
/// positional: time per tile, distance tiles.
///
/// Not done, because the client explains them nowhere the server can read: scaling the
/// animation time with distance (how the three differences combine, and what the speed
/// increase takes off), the shooter's cosmetic direction and bunny hop, and the seven internal
/// variables (tiles travelled, mid-flight collisions, animated position), which need the flight
/// simulated tile by tile. Their params are kept and ignored.
/// </summary>
[RoomObjectLogic("wf_xtra_rotate_to_dir")]
public class WiredAddonProjectile(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_ROTATE = 0;
    private const int PARAM_DIRECTIONAL_SYSTEM = 1;
    private const int PARAM_ROTATION_OFFSET = 10;
    private const int PARAM_DISTANCE_MODE = 14;
    private const int PARAM_DISTANCE_IS_VARIABLE = 15;
    private const int PARAM_DISTANCE_TILES = 16;
    private const int PARAM_DISTANCE_TARGET = 17;
    private const int PARAM_CURVE_STRENGTH = 18;

    private const int VARIABLE_DISTANCE = 1;
    private const int VARIABLE_COUNT = 2;

    private const int SLOT_PROJECTILES = 0;

    // The bounds of the editor's inputs.
    private const int TIME_PER_TILE_MIN_MS = 1;
    private const int TIME_PER_TILE_MAX_MS = 100000;
    private const int SPEED_INCREASE_MAX_MS = 100000;
    private const int ROTATION_OFFSET_MAX = 7;
    private const int VARIABLES_MASK_MAX = 127;
    private const int DISTANCE_TILES_MIN = -64;
    private const int DISTANCE_TILES_MAX = 64;
    private const int CURVE_STRENGTH_MIN = -1000;
    private const int CURVE_STRENGTH_MAX = 1000;

    public override int WiredCode => (int)WiredAddonType.PROJECTILE;

    protected override bool HasPositionalVariableIds => true;

    public override int GetMaxVariableIds() => VARIABLE_COUNT;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredEnumParamRule<WiredDirectionalSystemType>(
                WiredDirectionalSystemType.EightStraight
            ),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(0, TIME_PER_TILE_MAX_MS, TIME_PER_TILE_MIN_MS),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(0, SPEED_INCREASE_MAX_MS, 0),
            new WiredRangeParamRule(0, ROTATION_OFFSET_MAX, 0),
            new WiredRangeParamRule(0, VARIABLES_MASK_MAX, 0),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            new WiredEnumParamRule<WiredProjectileDistanceType>(WiredProjectileDistanceType.Normal),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(DISTANCE_TILES_MIN, DISTANCE_TILES_MAX, 0),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
            new WiredRangeParamRule(CURVE_STRENGTH_MIN, CURVE_STRENGTH_MAX, 0),
        ];

    // The projectiles, then where the time and the distance variables are read.
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.Furni, WiredSources.Furni, WiredSources.Furni];

    // Where the time variable is read, the shooter, where the distance variable is read.
    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users, WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        var projectiles = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_PROJECTILES));

        if (projectiles.Count == 0)
            return Task.FromResult(true);

        var distance = GetIntParamOrDefault(
            PARAM_DISTANCE_MODE,
            WiredProjectileDistanceType.Normal
        );
        long tiles = GetIntParamOrDefault(PARAM_DISTANCE_TILES, 0);

        // A distance variable nobody selected holds leaves the flight as long as the move.
        if (
            distance != WiredProjectileDistanceType.Normal
            && GetIntParamOrDefault(PARAM_DISTANCE_IS_VARIABLE, false)
            && !TryReadVariableOperand(
                VARIABLE_DISTANCE,
                PARAM_DISTANCE_TARGET,
                ctx.Selected,
                out tiles
            )
        )
            distance = WiredProjectileDistanceType.Normal;

        var curveStrength = GetIntParamOrDefault(PARAM_CURVE_STRENGTH, 0);

        ctx.Policy.Projectile = new WiredProjectileSettings
        {
            ProjectileIds = projectiles.Select(x => x.ObjectId.Value).ToHashSet(),
            RotateBy = GetIntParamOrDefault(PARAM_ROTATE, false)
                ? GetIntParamOrDefault(
                    PARAM_DIRECTIONAL_SYSTEM,
                    WiredDirectionalSystemType.EightStraight
                )
                : null,
            RotationOffset = GetIntParamOrDefault(PARAM_ROTATION_OFFSET, 0),
            CurveStrength = curveStrength == 0 ? null : curveStrength,
            Distance = distance,
            // A variable can hold anything; the client's input cannot.
            DistanceTiles = (int)Math.Clamp(tiles, DISTANCE_TILES_MIN, DISTANCE_TILES_MAX),
        };

        return Task.FromResult(true);
    }
}
