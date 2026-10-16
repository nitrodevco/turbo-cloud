using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Rules;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires when the picked variable is created, written or removed on any target. Int params, as
/// the client's editor saves them (<c>triggerconfs/VariableUpdate</c>): created, value changed and
/// deleted (0 or 1 each, <c>variables.trigger_options.0</c> to <c>.2</c>); the value-changed kinds as
/// a mask (bit 0 increased, bit 1 decreased, bit 2 unchanged; 0 is all of them); and the origins
/// allowed as a mask (bit n for <see cref="WiredVariableChangeOriginType"/> n, -1 all); and since
/// WIN63-202610121624 which variables are watched (<c>variables.trigger_mode</c>: 0 the one
/// picked in the editor, 1 those of the variable boxes picked as furni; Wired Faculty 12/10/2026:
/// "works on multiple variables at the same time by selecting the variable boxes"). The target
/// the change happened on becomes the triggering furni or user.
/// </summary>
[RoomObjectLogic("wf_trg_var_changed")]
public class WiredTriggerVariableChanged(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.VARIABLE_UPDATE;
    public override List<Type> SupportedEventTypes { get; } = [typeof(WiredVariableChangedEvent)];

    public override int GetMaxVariableIds() => 1;

    private const int PARAM_CREATED = 0;
    private const int PARAM_UPDATED = 1;
    private const int PARAM_REMOVED = 2;
    private const int PARAM_UPDATE_KINDS = 3;
    private const int PARAM_ORIGINS = 4;
    private const int PARAM_PICKED_BOXES = 5;

    private const int UPDATE_INCREASED = 1 << 0;
    private const int UPDATE_DECREASED = 1 << 1;
    private const int UPDATE_UNCHANGED = 1 << 2;
    private const int ALL_ORIGINS = -1;

    // A fresh box fires on created and on value changes of every kind (official client capture:
    // defaultIntParams [1,1,0,7,-1,0]).
    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(true),
            new WiredBoolParamRule(true),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(
                0,
                0b111,
                UPDATE_INCREASED | UPDATE_DECREASED | UPDATE_UNCHANGED
            ),
            new WiredRangeParamRule(ALL_ORIGINS, 0b1111, ALL_ORIGINS),
            new WiredBoolParamRule(false),
        ];

    /// <summary>The editor reads the origins and the mode as optional (<c>intParams.length > 4</c>).</summary>
    protected override int GetRequiredIntParamCount() => PARAM_ORIGINS;

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not WiredVariableChangedEvent change)
            return Task.FromResult(false);

        WiredVariableValue value = default;
        WiredVariableValue previous = default;
        var watched = false;

        foreach (var variable in GetWatchedVariables())
        {
            if (TryReadChange(variable, change, out value, out previous))
            {
                watched = true;

                break;
            }
        }

        if (!watched)
            return Task.FromResult(false);

        var origins = GetIntParamOrDefault(PARAM_ORIGINS, ALL_ORIGINS);

        if (origins != ALL_ORIGINS && (origins & (1 << (int)change.Origin)) == 0)
            return Task.FromResult(false);

        return Task.FromResult(
            change.ChangeType switch
            {
                WiredVariableChangeType.Created => GetIntParamOrDefault(PARAM_CREATED, false),
                WiredVariableChangeType.Removed => GetIntParamOrDefault(PARAM_REMOVED, false),
                WiredVariableChangeType.Updated => GetIntParamOrDefault(PARAM_UPDATED, false)
                    && MatchesUpdateKind(value, previous),
                _ => false,
            }
        );
    }

    /// <summary>
    /// The variable picked in the editor, or in "picked variable boxes" mode the variables of the
    /// variable boxes picked as this trigger's furni.
    /// </summary>
    private IEnumerable<IWiredVariable> GetWatchedVariables()
    {
        if (!GetIntParamOrDefault(PARAM_PICKED_BOXES, false))
        {
            if (GetVariable(0) is { } picked)
                yield return picked;

            yield break;
        }

        foreach (var stuffId in _wiredData.StuffIds)
        {
            if (
                FurniModule.TryGetItem(stuffId, out var item)
                && item.Logic is Variables.FurnitureWiredVariableLogic box
            )
                yield return box;
        }
    }

    /// <summary>
    /// The watched variable's value after and before the change: the change's own for the
    /// variable itself, and for a sub-variable of the one that changed ("xp.current_level" of a
    /// level-up add-on) what it reads from the parent's new and old values. Official wired fires
    /// on a level-up that way (Wired Faculty #help, 08/10/2026: "set a variable changed trigger on
    /// the current_level subvar, make sure to only check increased and decreased").
    /// </summary>
    private static bool TryReadChange(
        IWiredVariable watched,
        WiredVariableChangedEvent change,
        out WiredVariableValue value,
        out WiredVariableValue previous
    )
    {
        value = change.Value;
        previous = change.PreviousValue;

        var watchedId = watched.GetVarSnapshot().VariableId;

        if (watchedId == change.VariableId)
            return true;

        if (watched is not WiredSubVariable sub || sub.ParentId != change.VariableId)
            return false;

        var key = new WiredVariableKey(watchedId, change.TargetType, change.TargetId);

        sub.TryGetValueFor(change.Value, key, out value);
        sub.TryGetValueFor(change.PreviousValue, key, out previous);

        return true;
    }

    private bool MatchesUpdateKind(WiredVariableValue value, WiredVariableValue previous)
    {
        var kinds = GetIntParamOrDefault(PARAM_UPDATE_KINDS, 0);

        if (kinds == 0)
            return true;

        var kind =
            value.Value > previous.Value ? UPDATE_INCREASED
            : value.Value < previous.Value ? UPDATE_DECREASED
            : UPDATE_UNCHANGED;

        return (kinds & kind) != 0;
    }

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        if (ctx.Event is not WiredVariableChangedEvent change)
            return Task.FromResult(false);

        switch (change.TargetType)
        {
            case WiredVariableTargetType.Furni:
                ctx.Selected.SelectedFurniIds.Add(change.TargetId);
                break;
            case WiredVariableTargetType.User:
                ctx.Selected.SelectedAvatarIds.Add(change.TargetId);
                break;
        }

        return Task.FromResult(true);
    }
}
