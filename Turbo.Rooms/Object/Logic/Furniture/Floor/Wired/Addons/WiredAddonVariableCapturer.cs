using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Reads a value out of what a player said. The "says something" trigger keyword holds a
/// "#(name)" token; whatever the player typed in its place is written to the picked variable
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

    /// <summary>The token the keyword of the stack's "says" trigger holds for the captured word.</summary>
    public string CaptureToken => WiredPlaceholderText.Token(SIGIL, GetStringParam());

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    /// <summary>
    /// The name is refused as Habbo refuses one (<see cref="WiredNameRules.PlaceholderError"/>),
    /// and so is a variable another capturer of the stack already writes, whatever either is
    /// named: Habbo answers <c>wiredfurni.error.link.destination_conflict</c> ("Each capturer must
    /// be unique in this wired stack") for a second capturer on the same context variable, named
    /// "a" or "b", and saves two capturers named "a" on different variables (official client
    /// capture 2026-10-14, NOTES-iv-save-errors).
    /// </summary>
    public override Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        if (WiredNameRules.PlaceholderError(update.StringParam, showsAll: false) is { } error)
            throw new WiredSaveRefusedException(error);

        if (
            update.VariableIds is [var variableId, ..]
            && variableId.Length > 0
            && variableId != "0"
            && FurniModule
                .GetFloorItemsOnTile(_ctx.GetTileIdx())
                .Any(item =>
                    item.Logic is WiredAddonVariableCapturer other
                    && !ReferenceEquals(other, this)
                    && other._wiredData?.VariableIds is [var otherId, ..]
                    && otherId == variableId
                )
        )
            throw new WiredSaveRefusedException(WiredSaveErrors.CAPTURE_DESTINATION_CONFLICT);

        return base.ApplyWiredUpdateAsync(ctx, update, ct);
    }

    /// <summary>
    /// Whether what was typed at this box's marker lets the trigger fire: in text mode it must
    /// be a line of the variable's text connector (official client capture: a character missing
    /// from the connector stops the trigger); otherwise it must be a whole 64-bit number: "abc",
    /// "3.7" and a 20-digit number do not fire it, "-5", "+11" and "2147483648" do (official client
    /// capture 2026-10-25T03-16-09, LAB-W5 wlab9 addon.16.B5 2/2 and B4 4/4). A trailing
    /// exact-mode marker left empty does not stop it (inference: measured in text mode only).
    /// </summary>
    internal bool Accepts(string typed) =>
        (typed.Length == 0 && !GetIntParamOrDefault(0, false)) || TryGetValue(typed, out _);

    /// <summary>
    /// The value <paramref name="typed"/> stands for: in text mode the connector line whose text
    /// it is, exactly first, then ignoring case (official client capture); otherwise the number.
    /// </summary>
    private bool TryGetValue(string typed, out long value)
    {
        value = 0;

        if (!GetIntParamOrDefault(0, false))
            return long.TryParse(
                typed,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value
            );

        // A trailing exact-mode marker left empty takes the connector's "0=" line, if it has one
        // (official client capture).
        if (typed.Length == 0)
        {
            if (
                GetVariable(0) is not Variables.FurnitureWiredVariableLogic variable
                || variable.GetEmptyTextValue() is not { } empty
            )
                return false;

            value = empty.Value;

            return true;
        }

        var connectors = GetVariable(0)?.GetVarSnapshot().TextConnectors;

        if (connectors is null)
            return false;

        var connector = connectors.FirstOrDefault(x =>
            string.Equals(x.Value, typed, StringComparison.Ordinal)
        );

        if (connector.Value is null)
            connector = connectors.FirstOrDefault(x =>
                string.Equals(x.Value, typed, StringComparison.OrdinalIgnoreCase)
            );

        if (connector.Value is null)
            return false;

        value = connector.Key;

        return true;
    }

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override async Task BeforeEffectsAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        if (
            ctx.Event is not PlayerChatEvent chat
            || ctx.Trigger is not WiredTriggerHabboSaysKeyword trigger
        )
            return;

        var variable = GetVariable(0);

        if (
            variable is null
            || GetStringParam().Length == 0
            || !trigger.TryCapture((chat.UnfilteredText ?? chat.Text).Trim(), out var captures)
            || captures.FirstOrDefault(x => x.Capturer == this).Typed is not { } typed
            || !TryGetValue(typed, out var value)
        )
            return;

        var snapshot = variable.GetVarSnapshot();

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
