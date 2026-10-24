using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
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
/// The shooter (by default the triggering user) is turned to face the way the projectile flies,
/// in the same WiredMovements (official client capture 2026-10-24T07-42-18, a never-saved box
/// with Move Furni +2,0: the block turned to 2 and the clicker, facing 4 three tiles south of
/// it, got userDirections body 2 head 2, 1/1).
///
/// Not done, because the client explains them nowhere the server can read: scaling the
/// animation time with distance (how the three differences combine, and what the speed
/// increase takes off), the bunny hop, and the seven internal
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
    private const int PARAM_TURN_SHOOTER = 12;
    private const int PARAM_DISTANCE_MODE = 14;
    private const int PARAM_DISTANCE_IS_VARIABLE = 15;
    private const int PARAM_DISTANCE_TILES = 16;
    private const int PARAM_DISTANCE_TARGET = 17;
    private const int PARAM_CURVE_STRENGTH = 18;

    private const int VARIABLE_DISTANCE = 1;
    private const int VARIABLE_COUNT = 2;

    private const int SLOT_PROJECTILES = 0;
    private const int SLOT_SHOOTERS = 1;

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

    // A fresh box's values are the official server's (defaults [1,1,0,0,100,-20,1,1,0,4,0,0,1,1,
    // 0,0,1,-20,50], 2026-10-20T17-35-39-Open-wait).
    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(true),
            new WiredEnumParamRule<WiredDirectionalSystemType>(
                WiredDirectionalSystemType.EightDiffuse
            ),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(TIME_PER_TILE_MIN_MS, TIME_PER_TILE_MAX_MS, 100),
            WiredRules.VariableTarget(WiredVariableTargetType.Context),
            new WiredBoolParamRule(true),
            new WiredBoolParamRule(true),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(0, SPEED_INCREASE_MAX_MS, 4),
            new WiredRangeParamRule(0, ROTATION_OFFSET_MAX, 0),
            new WiredRangeParamRule(0, VARIABLES_MASK_MAX, 0),
            new WiredBoolParamRule(true),
            new WiredBoolParamRule(true),
            new WiredEnumParamRule<WiredProjectileDistanceType>(WiredProjectileDistanceType.Normal),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(DISTANCE_TILES_MIN, DISTANCE_TILES_MAX, 1),
            WiredRules.VariableTarget(WiredVariableTargetType.Context),
            new WiredRangeParamRule(CURVE_STRENGTH_MIN, CURVE_STRENGTH_MAX, 50),
        ];

    // The projectiles, then where the time and the distance variables are read. The projectiles
    // default to every furni the stack moves; the time and distance slots to the
    // triggering furni (official form contract: [0,100,200,201,901] default 901, then
    // [0,100,200,201] x2 default 0).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [WiredFurniSourceType.MovedItems, .. WiredSources.Furni],
            WiredSources.Furni.DefaultingTo(WiredFurniSourceType.TriggeredItem),
            WiredSources.Furni.DefaultingTo(WiredFurniSourceType.TriggeredItem),
        ];

    // Where the time variable is read, the shooter, where the distance variable is read.
    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users, WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        var projectiles = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_PROJECTILES));
        var allMoved = GetFurniSources()[SLOT_PROJECTILES]
            .Contains(WiredFurniSourceType.MovedItems);

        if (projectiles.Count == 0 && !allMoved)
            return Task.FromResult(true);

        // Two Projectiles: the lower box's applies, curve 50 below 300 curved 50 (official
        // client capture 2026-10-24, W1 lab addon.21.R2, 1/1; each alone 50 / 300).
        if (!IsFirstOfKind(ctx))
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
        var directions = GetIntParamOrDefault(
            PARAM_DIRECTIONAL_SYSTEM,
            WiredDirectionalSystemType.EightStraight
        );

        ctx.Policy.Projectile = new WiredProjectileSettings
        {
            ProjectileIds = projectiles.Select(x => x.ObjectId.Value).ToHashSet(),
            AllMoved = allMoved,
            RotateBy = GetIntParamOrDefault(PARAM_ROTATE, false) ? directions : null,
            RotationOffset = GetIntParamOrDefault(PARAM_ROTATION_OFFSET, 0),
            CurveStrength = curveStrength == 0 ? null : curveStrength,
            Distance = distance,
            // A variable can hold anything; the client's input cannot.
            DistanceTiles = (int)Math.Clamp(tiles, DISTANCE_TILES_MIN, DISTANCE_TILES_MAX),
            ShooterIds = GetIntParamOrDefault(PARAM_TURN_SHOOTER, false)
                ? WiredSlotSelection
                    .ForUserSlot(this, ctx, SLOT_SHOOTERS)
                    .SelectedAvatarIds.ToHashSet()
                : new HashSet<RoomObjectId>(),
            ShooterDirections = directions,
        };

        return Task.FromResult(true);
    }
}
