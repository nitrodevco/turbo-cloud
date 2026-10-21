using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Link;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Link;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// WIRED Effect: Send Link Dataframe (<c>wf_act_send_dataframe</c>, action code 64): "Send up to
/// 12 named values through the selected WIRED Links" (<c>wiredfurni.params.link.Send_info</c>).
/// As the AS3 editor writes it (WIN63-202610201032 <c>LinkDataframeSendPreset.as</c>): int 0 the
/// number of rows, then per row its value source (0 a number, 1 a variable's value, 2 a variable's
/// text, 3 a custom message), its number and the variable's holder type (furni, user, global,
/// context); the text is the rows' <c>[key, message]</c> pairs as JSON; the twelve variable ids are
/// the rows' variables, by position. Furni slot 0 holds the Links; row n reads its variable on
/// furni slot n + 1 and user slot n (<c>SendLinkDataframe.mergedSelections</c>).
/// </summary>
[RoomObjectLogic("wf_act_send_dataframe")]
public class WiredActionSendDataframe(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public const int MODE_NUMBER = 0;
    public const int MODE_VARIABLE_VALUE = 1;
    public const int MODE_VARIABLE_TEXT = 2;
    public const int MODE_MESSAGE = 3;

    private const int INTS_PER_ROW = 3;

    public override int WiredCode => (int)WiredActionType.SEND_LINK_DATAFRAME;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, WiredLinkBoxes.MAX_ROWS, 0),
            .. Enumerable
                .Range(0, WiredLinkBoxes.MAX_ROWS)
                .SelectMany(_ =>
                    new IWiredParamRule[]
                    {
                        new WiredRangeParamRule(MODE_NUMBER, MODE_MESSAGE, MODE_NUMBER),
                        WiredRules.AnyInt(),
                        // A fresh row reads a context variable (recorded: target -20).
                        WiredRules.VariableTarget(WiredVariableTargetType.Context),
                    }
                ),
        ];

    // A save with fewer row ints is padded (recorded: 36 ints saved, 37 read back).
    protected override int GetRequiredIntParamCount() => 1;

    // Recorded: allowWallFurni true.
    protected override bool AllowsWallFurni => true;

    public override int GetMaxVariableIds() => WiredLinkBoxes.MAX_ROWS;

    protected override bool HasPositionalVariableIds => true;

    protected override int GetStringParamMaxLength() => WiredLinkDataframe.MAX_TEXT_BYTES;

    // The Links, then each row's variable holders: furni slot n + 1 and user slot n (recorded:
    // furni [100, 200 x 12] of [0,100,101,200,201], users 12 x 0 of [0,200,201]).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.FurniOrSecondary,
            .. Enumerable
                .Range(0, WiredLinkBoxes.MAX_ROWS)
                .Select(_ =>
                    WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.SelectorItems)
                ),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [.. Enumerable.Range(0, WiredLinkBoxes.MAX_ROWS).Select(_ => WiredSources.Users)];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        [.. AllVariablesContext(), .. TextPlaceholdersContext()];

    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        slot == 0 && !WiredLinkBoxes.IsLink(item) ? WiredLinkBoxes.REQUIRE_LINK_FURNI : null;

    public override Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        if (Refusal(update) is { } refusal)
            throw new WiredSaveRefusedException(refusal.Key, refusal.Parameters);

        return base.ApplyWiredUpdateAsync(ctx, update, ct);
    }

    /// <summary>What the editor would refuse in this save (AS3 <c>LinkDataframeSendPreset.validationError</c>).</summary>
    private static (string Key, (string, string)[] Parameters)? Refusal(UpdateWiredMessage update)
    {
        var count = update.IntParams.Count > 0 ? update.IntParams[0] : -1;

        if (
            update.IntParams.Count is 0 or > 1 + (WiredLinkBoxes.MAX_ROWS * INTS_PER_ROW)
            || update.IntParams.Count < 1 + (count * INTS_PER_ROW)
            || update.VariableIds.Count > WiredLinkBoxes.MAX_ROWS
            || WiredLinkDataframe.Decode(update.StringParam, count) is not { } rows
        )
            return (WiredLinkDataframe.INVALID_DATAFRAME, []);

        var keys = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < rows.Count; i++)
        {
            var (key, message) = rows[i];

            if (WiredLinkDataframe.KeyError(key) is { } keyError)
                return (keyError, []);

            // Recorded: a 201-byte custom message, "too much text" with limit 200.
            if (
                update.IntParams[1 + (i * INTS_PER_ROW)] == MODE_MESSAGE
                && WiredLinkBoxes.Utf8Length(message) > WiredLinkDataframe.MAX_MESSAGE_BYTES
            )
                return (
                    WiredActionSendMessage.TOO_MUCH_TEXT,
                    [
                        (
                            "limit",
                            WiredLinkDataframe.MAX_MESSAGE_BYTES.ToString(
                                CultureInfo.InvariantCulture
                            )
                        ),
                    ]
                );

            if (!keys.Add(WiredLinkDataframe.NormalizeKey(key)))
                return (WiredLinkDataframe.NAME_UNIQUE, []);
        }

        return
            WiredLinkDataframe.StorageError(
                update.StringParam,
                update.IntParams,
                update.VariableIds
            )
                is { } storage
            ? (storage, [])
            : null;
    }

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var links = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 0));
        var count = GetIntParamOrDefault(0, 0);

        if (
            links.Count == 0
            || WiredLinkDataframe.Decode(_wiredData?.StringParam, count) is not { } rows
        )
            return false;

        var values = ImmutableArray.CreateBuilder<WiredLinkDataframeRow>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            if (await RowValueAsync(ctx, i, rows[i].Text, ct) is { } value)
                values.Add(
                    new WiredLinkDataframeRow(WiredLinkDataframe.NormalizeKey(rows[i].Key), value)
                );
        }

        var payload = new WiredLinkPayload
        {
            SourceRoomId = _roomGrain.RoomId,
            Rows = values.ToImmutable(),
        };

        return await WiredLinkBoxes.SendAsync(_grainFactory, _roomGrain.RoomId, links, payload, ct)
            > 0;
    }

    /// <summary>
    /// A row's value as text: its number, its variable's value or text on the first holder of
    /// its selection that has one, or its message with the stack's placeholders filled. Null when
    /// the variable has no holder with a value, which leaves the key out of the dataframe.
    /// </summary>
    private async Task<string?> RowValueAsync(
        IWiredExecutionContext ctx,
        int row,
        string message,
        CancellationToken ct
    )
    {
        var first = 1 + (row * INTS_PER_ROW);
        var mode = GetIntParamOrDefault(first, MODE_NUMBER);

        if (mode == MODE_NUMBER)
            return GetIntParamOrDefault(first + 1, 0).ToString(CultureInfo.InvariantCulture);

        if (mode == MODE_MESSAGE)
            return WiredLinkBoxes.TruncateUtf8(
                await ctx.FormatTextAsync(message, ct),
                WiredLinkDataframe.MAX_MESSAGE_BYTES
            );

        if (GetVariable(row) is not { } variable)
            return mode == MODE_VARIABLE_TEXT ? string.Empty : null;

        var targetType = GetIntParamOrDefault(first + 2, WiredVariableTargetType.Context);
        var selection = WiredSlotSelection
            .ForSlot(this, ctx, row + 1)
            .UnionWith(WiredSlotSelection.ForUserSlot(this, ctx, row));

        foreach (var targetId in GetTargetIds(targetType, selection))
        {
            if (ReadVariable(variable, targetType, targetId) is not { } value)
                continue;

            if (mode == MODE_VARIABLE_VALUE)
                return value.Value.ToString(CultureInfo.InvariantCulture);

            var snapshot = variable.GetVarSnapshot();

            return snapshot.TextConnectors.TryGetValue(value.Value, out var label)
                ? label
                : snapshot.TextConnectorFallback
                    ?? value.Value.ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }
}
