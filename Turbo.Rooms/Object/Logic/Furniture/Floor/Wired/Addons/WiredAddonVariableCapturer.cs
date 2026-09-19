using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Reads a value out of what a player said. The "says something" trigger keyword holds a
/// "#name" token; whatever the player typed in its place is written to the picked variable
/// on the speaker. Param 0 is text mode: the typed word is looked up among the text
/// connector labels of the variable instead of being parsed as a number.
/// </summary>
[RoomObjectLogic("wf_xtra_text_input_variable")]
public class WiredAddonVariableCapturer(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private const char SIGIL = '#';

    public override int WiredCode => (int)WiredAddonType.VARIABLE_CAPTURER;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        [
            new WiredVariableAllInRoomSnapshot()
            {
                ContextType = WiredContextType.AllVariablesInRoom,
                AllVariablesHash = _roomGrain._state.AllVariablesHash,
            },
        ];

    public override async Task BeforeEffectsAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        if (
            ctx.Event is not PlayerChatEvent chat
            || ctx.Trigger is not WiredTriggerHabboSaysKeyword trigger
        )
            return;

        var variable = GetVariable(0);
        var name = _wiredData.StringParam?.Trim() ?? string.Empty;

        if (variable is null || name.Length == 0)
            return;

        var keyword = trigger.GetSnapshot().StringParam?.Trim() ?? string.Empty;
        var token = SIGIL + name;
        var tokenIndex = keyword.IndexOf(token, StringComparison.OrdinalIgnoreCase);

        if (tokenIndex < 0)
            return;

        var pattern =
            Regex.Escape(keyword[..tokenIndex]).Replace("\\ ", "\\s+")
            + "(\\S+)"
            + Regex.Escape(keyword[(tokenIndex + token.Length)..]).Replace("\\ ", "\\s+");
        var match = Regex.Match(chat.Text.Trim(), pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
            return;

        var typed = match.Groups[1].Value;
        var snapshot = variable.GetVarSnapshot();
        int value;

        if (GetIntParamOrDefault(0, false))
        {
            var connector = snapshot.TextConnectors.FirstOrDefault(x =>
                string.Equals(x.Value, typed, StringComparison.OrdinalIgnoreCase)
            );

            if (connector.Value is null)
                return;

            value = connector.Key;
        }
        else if (!int.TryParse(typed, out value))
        {
            return;
        }

        foreach (var targetId in GetTargetIds(snapshot.TargetType, ctx.Selected))
        {
            var key = new WiredVariableKey(snapshot.VariableId, snapshot.TargetType, targetId);

            if (variable.TryGetValue(key, out _))
                await variable.SetValueAsync(
                    new Turbo.Rooms.Wired.WiredExecutionContext(_roomGrain),
                    key,
                    value
                );
            else
                await variable.GiveValueAsync(key, value, true);
        }
    }
}
