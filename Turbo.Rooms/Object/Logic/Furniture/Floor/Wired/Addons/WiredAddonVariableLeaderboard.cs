using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired.Leaderboard;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// WIRED Variable Leaderboard (code 1005, released 2026-10-13): shows the user variables stacked
/// with it as a leaderboard panel in the room. Its settings are the client's
/// <c>VariableLeaderboardConfigCodec</c> (<see cref="VariableLeaderboardConfig"/>); a saved box
/// is published, and the room's <see cref="RoomVariableLeaderboardSystem"/> tells viewers about it.
/// The variables it offers are the user variable boxes on its tile, the bottom ten
/// (<c>wiredfurni.params.variable_leaderboard.warning.too_many_choices</c>).
/// </summary>
[RoomObjectLogic("wf_xtra_var_leaderboard")]
public class WiredAddonVariableLeaderboard(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    public const int MAX_CHOICES = 10;
    public const string ERROR_INVALID =
        "wiredfurni.error.variable_leaderboard.invalid_configuration";
    public const string ERROR_ROOM_LIMIT = "wiredfurni.error.variable_leaderboard.room_limit";
    public const string ERROR_TOO_MANY_CHOICES =
        "wiredfurni.error.variable_leaderboard.too_many_choices";

    private VariableLeaderboardConfig? _config;
    private bool _configRead;

    public override int WiredCode => (int)WiredAddonType.VARIABLE_LEADERBOARD;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            .. VariableLeaderboardConfig.UnpublishedIntParams.Select(value =>
                WiredRules.AnyInt(value)
            ),
        ];

    public override int GetMaxVariableIds() => VariableLeaderboardConfig.VARIABLE_ID_COUNT;

    protected override int GetMaxIntParams() => VariableLeaderboardConfig.INT_PARAM_COUNT;

    protected override bool HasPositionalVariableIds => true;

    /// <summary>The saved settings, or null while the box is unpublished (or its data is bad).</summary>
    public VariableLeaderboardConfig? Config
    {
        get
        {
            if (!_configRead && _wiredData is not null)
            {
                _config = VariableLeaderboardConfig.Decode(
                    _wiredData.IntParams,
                    _wiredData.VariableIds,
                    _wiredData.StringParam,
                    out _
                );
                _configRead = true;
            }

            return _config;
        }
    }

    public bool IsPublished => Config is not null;

    public override async Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        var config = VariableLeaderboardConfig.Decode(
            update.IntParams,
            update.VariableIds,
            update.StringParam,
            out var invalidReason
        );

        if (config is null && invalidReason is not null)
            throw new WiredSaveRefusedException(ERROR_INVALID);

        if (config is not null)
        {
            if (GetStackedUserVariables().Count > MAX_CHOICES)
                throw new WiredSaveRefusedException(ERROR_TOO_MANY_CHOICES);

            var published = FurniModule.Items.Count(item =>
                item.ObjectId != _ctx.ObjectId.Value
                && item.Logic is WiredAddonVariableLeaderboard { IsPublished: true }
            );

            if (published >= RoomVariableLeaderboardSystem.MAX_OVERLAYS)
                throw new WiredSaveRefusedException(ERROR_ROOM_LIMIT);
        }

        _configRead = false;

        return await base.ApplyWiredUpdateAsync(ctx, update, ct);
    }

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        _configRead = false;

        await base.FillInternalDataAsync(ct);
    }

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots()
    {
        var stacked = GetStackedUserVariables();
        var (x, y) = (_ctx.RoomObject.X, _ctx.RoomObject.Y);

        return
        [
            .. AllVariablesContext(),
            new WiredVariableLeaderboardContextSnapshot
            {
                ContextType = WiredContextType.VariableLeaderboard,
                OverlayInstanceId = VariableLeaderboardSystem.GetOverlayInstanceId(this),
                TileX = x,
                TileY = y,
                TotalCandidateCount = stacked.Count,
                Choices =
                [
                    .. stacked
                        .Take(MAX_CHOICES)
                        .Select(box =>
                        {
                            var snapshot = box.GetVarSnapshot();

                            return new WiredVariableLeaderboardChoiceSnapshot
                            {
                                VariableId = snapshot.VariableId.ToString(),
                                TechnicalName = snapshot.VariableName,
                                Persistent = snapshot.AvailabilityType.IsPermanent(),
                                HasValue = snapshot.Flags.HasFlag(WiredVariableFlags.HasValue),
                                ApplicableLevelUpCount = GetLevelUps(box).Count,
                            };
                        }),
                ],
            },
        ];
    }

    /// <summary>The user variable boxes stacked with this one, bottom first.</summary>
    public List<WiredVariableUser> GetStackedUserVariables() =>
        [
            .. FurniModule
                .GetFloorItemsOnTile(_ctx.GetTileIdx())
                .OrderBy(item => item.Z.Value)
                .Select(item => item.Logic)
                .OfType<WiredVariableUser>()
                .Where(box => !string.IsNullOrWhiteSpace(box.GetVarSnapshot().VariableName)),
        ];

    /// <summary>
    /// The Level Up add-ons on this tile that level <paramref name="box"/>: a Level Up add-on
    /// levels the variable box it finds first on its tile.
    /// </summary>
    public List<WiredAddonVariableLevelUp> GetLevelUps(FurnitureWiredVariableLogic box)
    {
        var tile = FurniModule.GetFloorItemsOnTile(_ctx.GetTileIdx()).ToList();
        var first = tile.Select(item => item.Logic)
            .OfType<FurnitureWiredVariableLogic>()
            .FirstOrDefault();

        return first == box
            ? [.. tile.Select(item => item.Logic).OfType<WiredAddonVariableLevelUp>()]
            : [];
    }

    /// <summary>The stacked variables a published leaderboard offers its viewers: the persisted ones.</summary>
    public ImmutableArray<WiredVariableUser> GetChoices() =>
        [
            .. GetStackedUserVariables()
                .Take(MAX_CHOICES)
                .Where(box => box.GetVarSnapshot().AvailabilityType.IsPermanent()),
        ];
}
