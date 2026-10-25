using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when the picked furni stand as they did when the box was saved. The four params choose
/// which aspects count: state, direction, position, altitude.
/// </summary>
[RoomObjectLogic("wf_cnd_match_snapshot")]
public class WiredConditionItemMatches(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.STATES_MATCH;

    protected override bool AllowsWallFurni => true;

    /// <summary>
    /// "All of the furni match" (0) or "any of the furni match" (1); Habbo opens the box and its
    /// negative twin with quantifierType 1 (official client capture of codes 0 and 13).
    /// </summary>
    protected override WiredQuantifierType QuantifierType => WiredQuantifierType.Furni;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false), // state
            new WiredBoolParamRule(false), // direction
            new WiredBoolParamRule(false), // position
            new WiredBoolParamRule(false), // altitude
        ];

    // The furni compared with their snapshot: the picked ones by default, or the triggering,
    // selector or signal furni among them (official client capture of the box's sources,
    // [0,100,200,201] default 100).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override bool KeepsFurniSnapshot => true;

    // The snapshot in the editor's text, the parts this box leaves out as "N" (official client
    // capture 2026-10-24).
    protected override string GetEditorStringParam() =>
        FormatFurniSnapshot(
            GetIntParamOrDefault(0, false),
            GetIntParamOrDefault(1, false),
            GetIntParamOrDefault(2, false),
            GetIntParamOrDefault(3, false)
        );

    /// <summary>
    /// The snapshot entries to compare: all of them for the picked furni, otherwise those of the
    /// slot's furni (inference: only a furni the box has a snapshot of can be compared).
    /// </summary>
    private Dictionary<int, WiredFurniSnapshotEntry> SnapshotToCompare(IWiredProcessingContext ctx)
    {
        var snapshot = GetFurniSnapshot();

        if (GetFurniSources()[0].Contains(WiredFurniSourceType.SelectedItems))
            return snapshot;

        var furni = WiredSlotSelection.ForSlot(this, ctx, 0).SelectedFurniIds;

        return snapshot.Where(x => furni.Contains(x.Key)).ToDictionary();
    }

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        // With nothing to compare "all match" holds and "any matches" does not: saved with no
        // picks, quantifier all, it held (official client capture 2026-10-24, wired matrix
        // condition.0.R3, 2/2; "any" and the slot sources inferred from the other quantified
        // conditions).
        var snapshot = SnapshotToCompare(ctx);
        var checkState = GetIntParamOrDefault(0, false);
        var checkRotation = GetIntParamOrDefault(1, false);
        var checkPosition = GetIntParamOrDefault(2, false);
        var checkAltitude = GetIntParamOrDefault(3, false);

        // A furni no longer in the room does not match its snapshot.
        bool Matches(int itemId, WiredFurniSnapshotEntry entry) =>
            TryGetFloorItem(itemId, out var item)
            && (!checkState || item.Logic.GetState() == entry.State)
            && (!checkRotation || (int)item.Rotation == entry.Rotation)
            && (!checkPosition || (item.X == entry.X && item.Y == entry.Y))
            && (!checkAltitude || item.Z.ToInt() == entry.Z);

        return Quantify(snapshot.Select(x => Matches(x.Key, x.Value)), QuantifierRequiresAll);
    }
}
