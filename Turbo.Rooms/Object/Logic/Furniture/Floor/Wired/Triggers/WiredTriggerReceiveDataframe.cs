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
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Link;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Link;
using Turbo.Rooms.Wired.Rules;
using Turbo.Rooms.Wired.Storage;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// WIRED Trigger: Receive Link Dataframe (<c>wf_trg_receive_dataframe</c>, trigger code 30): fires
/// when a Send Link Dataframe reaches one of the WIRED Links it picked, and captures the incoming
/// values by key: "Match incoming keys to text placeholders or writable context variables"
/// (<c>wiredfurni.params.link.Receive_info</c>). As the AS3 editor writes it (WIN63-202610201032
/// <c>LinkDataframeReceivePreset.as</c>): int 0 the number of rows (0 to 12), then one int per row,
/// its kind (0 text placeholder, 1 context variable); the text is the rows' <c>[key, placeholder]</c>
/// pairs as JSON; the twelve variable ids are the rows' context variables, by position.
/// </summary>
[RoomObjectLogic("wf_trg_receive_dataframe")]
public class WiredTriggerReceiveDataframe(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public const int KIND_PLACEHOLDER = 0;
    public const int KIND_VARIABLE = 1;

    public override int WiredCode => (int)WiredTriggerType.RECEIVE_LINK_DATAFRAME;

    public override List<Type> SupportedEventTypes { get; } = [typeof(WiredLinkReceivedEvent)];

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, WiredLinkBoxes.MAX_ROWS, 0),
            .. Enumerable
                .Range(0, WiredLinkBoxes.MAX_ROWS)
                .Select(_ => new WiredRangeParamRule(
                    KIND_PLACEHOLDER,
                    KIND_VARIABLE,
                    KIND_PLACEHOLDER
                )),
        ];

    public override int GetMaxVariableIds() => WiredLinkBoxes.MAX_ROWS;

    protected override bool HasPositionalVariableIds => true;

    protected override int GetStringParamMaxLength() => WiredLinkDataframe.MAX_TEXT_BYTES;

    // Recorded: default 100 (picked), allowed [100,200].
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    // A save with fewer row ints is padded (recorded: 12 ints saved, 13 read back).
    protected override int GetRequiredIntParamCount() => 1;

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        WiredLinkBoxes.IsLink(item) ? null : WiredLinkBoxes.REQUIRE_LINK_FURNI;

    public override Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        if (Refusal(update) is { } errorKey)
            throw new WiredSaveRefusedException(errorKey);

        return base.ApplyWiredUpdateAsync(ctx, update, ct);
    }

    /// <summary>What the editor would refuse in this save (AS3 <c>LinkDataframeReceivePreset.validationError</c>).</summary>
    private string? Refusal(UpdateWiredMessage update)
    {
        var count = update.IntParams.Count > 0 ? update.IntParams[0] : -1;

        if (
            update.IntParams.Count is 0 or > WiredLinkBoxes.MAX_ROWS + 1
            || update.IntParams.Count < count + 1
            || update.VariableIds.Count > WiredLinkBoxes.MAX_ROWS
            || WiredLinkDataframe.Decode(update.StringParam, count) is not { } rows
        )
            return WiredLinkDataframe.INVALID_DATAFRAME;

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var destinations = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < rows.Count; i++)
        {
            var (key, placeholder) = rows[i];
            var kind = update.IntParams[i + 1];

            if (WiredLinkDataframe.KeyError(key) is { } keyError)
                return keyError;

            string destination;

            if (kind == KIND_PLACEHOLDER)
            {
                if (WiredLinkDataframe.PlaceholderError(placeholder) is { } placeholderError)
                    return placeholderError;

                destination = "text:" + placeholder.ToLowerInvariant();
            }
            else
            {
                if (i >= update.VariableIds.Count || !IsCaptureVariable(update.VariableIds[i]))
                    return WiredLinkDataframe.INVALID_CAPTURE_VARIABLE;

                destination = "variable:" + update.VariableIds[i];
            }

            if (!keys.Add(WiredLinkDataframe.NormalizeKey(key)))
                return WiredLinkDataframe.NAME_UNIQUE;

            if (!destinations.Add(destination))
                return WiredLinkDataframe.DESTINATION_CONFLICT;
        }

        return WiredLinkDataframe.StorageError(
            update.StringParam,
            update.IntParams,
            update.VariableIds
        );
    }

    /// <summary>A context variable, which a row may write (AS3 <c>LinkDataframeReceiveRowPreset.variableFilter</c>: target -20).</summary>
    private bool IsCaptureVariable(string id) =>
        WiredVariableId.TryParse(id, out var variableId)
        && WiredSystem.GetVariableById(variableId) is { } variable
        && variable.GetVarSnapshot().TargetType == WiredVariableTargetType.Context;

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct) =>
        Task.FromResult(
            evt is WiredLinkReceivedEvent { Payload.IsDataframe: true } received
                && WiredLinkBoxes.ListensTo(this, received)
        );

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is WiredLinkReceivedEvent);

    /// <summary>The placeholders this box fills, for the message editors of its stack.</summary>
    public IEnumerable<string> PlaceholderNames() =>
        Rows()
            .Where(x => x.Kind == KIND_PLACEHOLDER)
            .Select(x => x.Destination)
            .Where(x => x.Length > 0);

    /// <summary>
    /// Writes the values of <paramref name="payload"/> whose keys this box captures into the
    /// stack's placeholders and context variables. A context variable takes a whole number only;
    /// a key the dataframe does not carry leaves its destination as it is.
    /// </summary>
    public async Task CaptureAsync(
        WiredLinkPayload payload,
        Dictionary<string, string> placeholders,
        KeyValueStore contextValues
    )
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var row in payload.Rows)
            values.TryAdd(WiredLinkDataframe.NormalizeKey(row.Key), row.Value);

        var index = 0;

        foreach (var (key, kind, destination) in Rows())
        {
            var slot = index++;

            if (!values.TryGetValue(key, out var value))
                continue;

            if (kind == KIND_PLACEHOLDER)
            {
                placeholders[WiredPlaceholderText.Token('$', destination)] = value;

                continue;
            }

            if (
                GetVariable(slot) is not { } variable
                || !long.TryParse(
                    value,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var number
                )
            )
                continue;

            await contextValues.GiveValueAsync(
                new WiredVariableKey(
                    variable.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.Context,
                    0
                ),
                new WiredVariableValue(number),
                true
            );
        }
    }

    /// <summary>The saved rows: normalised key, kind and placeholder name.</summary>
    private List<(string Key, int Kind, string Destination)> Rows()
    {
        var count = GetIntParamOrDefault(0, 0);

        if (WiredLinkDataframe.Decode(_wiredData?.StringParam, count) is not { } rows)
            return [];

        return
        [
            .. rows.Select(
                (row, i) =>
                    (
                        WiredLinkDataframe.NormalizeKey(row.Key),
                        GetIntParamOrDefault(i + 1, KIND_PLACEHOLDER),
                        row.Text.ToLowerInvariant()
                    )
            ),
        ];
    }
}
