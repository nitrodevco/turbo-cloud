using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Replaces "$(name)" in the text of the stack actions with the value of the picked variable on
/// the triggering target. Params: show every target (joined by the delimiter), the variable
/// target, and text mode, which prints the text connector label of the value instead.
/// </summary>
[RoomObjectLogic("wf_xtra_text_output_variable")]
public class WiredAddonVariablePlaceholder(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx), IWiredTextPlaceholder
{
    private const char SIGIL = '$';

    public override int WiredCode => (int)WiredAddonType.VARIABLE_PLACEHOLDER;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
            new WiredBoolParamRule(false),
        ];

    // Its holders come from one source, the furni or the user kind as the variable's target is
    // (AS3: mergedSelections [[0, 0]]); the triggering ones first, as it read before it had a
    // source of its own.
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.TriggeredItem,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.SelectedItems,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.TextPlaceholders.Add(this);

        return Task.FromResult(true);
    }

    public string Token =>
        WiredPlaceholderText.SplitNameAndDelimiter(_wiredData.StringParam).Item1
            is { Length: > 0 } name
            ? WiredPlaceholderText.Token(SIGIL, name)
            : string.Empty;

    public Task<string> ApplyAsync(IWiredExecutionContext ctx, string text, CancellationToken ct)
    {
        var (name, delimiter) = WiredPlaceholderText.SplitNameAndDelimiter(_wiredData.StringParam);
        var variable = GetVariable(0);

        if (
            variable is null
            || name.Length == 0
            || !text.Contains(WiredPlaceholderText.Token(SIGIL, name), StringComparison.Ordinal)
        )
            return Task.FromResult(text);

        var snapshot = variable.GetVarSnapshot();
        var targetType = GetTargetType(variable, 1);
        var textMode = GetIntParamOrDefault(2, false);
        var values = new List<string>();

        var holders = targetType switch
        {
            WiredVariableTargetType.User => WiredSlotSelection.ForUserSlot(this, ctx, 0),
            WiredVariableTargetType.Furni => WiredSlotSelection.ForSlot(this, ctx, 0),
            _ => ctx.Selected,
        };

        foreach (var targetId in GetTargetIds(targetType, holders))
        {
            if (
                !variable.TryGetValue(
                    new WiredVariableKey(snapshot.VariableId, targetType, targetId),
                    out var value
                )
            )
                continue;

            values.Add(
                textMode && snapshot.TextConnectors.TryGetValue(value, out var label)
                    ? label
                    : value.Value.ToString(CultureInfo.InvariantCulture)
            );

            if (!GetIntParamOrDefault(0, false))
                break;
        }

        return Task.FromResult(
            text.Replace(
                WiredPlaceholderText.Token(SIGIL, name),
                string.Join(delimiter, values),
                StringComparison.Ordinal
            )
        );
    }
}
