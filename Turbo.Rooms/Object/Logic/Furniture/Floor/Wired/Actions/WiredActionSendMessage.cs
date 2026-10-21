using System.Collections.Generic;
using System.Globalization;
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
/// WIRED Effect: Send Link Message (<c>wf_act_send_message</c>, action code 63): sends a message
/// through the picked WIRED Links to every room their networks reach, where Receive Link Message
/// fires with it in <c>$(trigger_text)</c>. Param 0 is the message source (AS3 WIN63-202610201032
/// <c>SendLinkMessage.as</c>: 0 "From WIRED trigger", the chat or received message that fired the
/// stack; 1 "Custom message", the box's text with its placeholders filled). A message holds at
/// most <see cref="Configuration.WiredConfig.LinkMessageMaxBytes"/> UTF-8 bytes; a longer save is
/// refused with <c>wiredfurni.error.too_much_text</c> (official client capture 2026-10-21).
/// </summary>
[RoomObjectLogic("wf_act_send_message")]
public class WiredActionSendMessage(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public const int SOURCE_TRIGGER = 0;
    public const int SOURCE_CUSTOM = 1;

    /// <summary>"Too much text" with its %limit% (a message over the byte limit, recorded with limit 1000).</summary>
    public const string TOO_MUCH_TEXT = "wiredfurni.error.too_much_text";

    /// <summary>What "From WIRED trigger" sends when the trigger carried no text (recorded: a walk-on trigger sent "null").</summary>
    public const string NO_TRIGGER_TEXT = "null";

    public override int WiredCode => (int)WiredActionType.SEND_LINK_MESSAGE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            // Any other source is refused with the dataframe's text (recorded, sic).
            new WiredRangeParamRule(SOURCE_TRIGGER, SOURCE_CUSTOM, SOURCE_TRIGGER)
            {
                ErrorKey = WiredLinkDataframe.INVALID_DATAFRAME,
            },
        ];

    // The Links to send through ("WIRED Links", wiredfurni.params.link.endpoints): picked by
    // default, or a selector's, a signal's or the triggering furni (recorded [0,100,200,201]).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override int GetStringParamMaxLength() => _roomGrain._wiredConfig.LinkMessageMaxBytes;

    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        WiredLinkBoxes.IsLink(item) ? null : WiredLinkBoxes.REQUIRE_LINK_FURNI;

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        TextPlaceholdersContext();

    public override Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        if (
            WiredLinkBoxes.Utf8Length(update.StringParam ?? string.Empty)
            > _roomGrain._wiredConfig.LinkMessageMaxBytes
        )
            throw new WiredSaveRefusedException(
                TOO_MUCH_TEXT,
                (
                    "limit",
                    _roomGrain._wiredConfig.LinkMessageMaxBytes.ToString(
                        CultureInfo.InvariantCulture
                    )
                )
            );

        return base.ApplyWiredUpdateAsync(ctx, update, ct);
    }

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var links = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 0));

        if (links.Count == 0)
            return false;

        var message =
            GetIntParamOrDefault(0, SOURCE_TRIGGER) == SOURCE_CUSTOM
                ? await ctx.FormatTextAsync(GetStringParam(), ct)
                : TriggerText(ctx);

        if (message.Length == 0)
            return false;

        var payload = new WiredLinkPayload
        {
            SourceRoomId = _roomGrain.RoomId,
            Message = WiredLinkBoxes.TruncateUtf8(
                message,
                _roomGrain._wiredConfig.LinkMessageMaxBytes
            ),
        };

        return await WiredLinkBoxes.SendAsync(_grainFactory, _roomGrain.RoomId, links, payload, ct)
            > 0;
    }

    /// <summary>What fired the stack said or received: its <c>$(trigger_text)</c>, "null" without one.</summary>
    private static string TriggerText(IWiredExecutionContext ctx) =>
        ctx is WiredContext wired
        && wired.CarriedPlaceholders.TryGetValue(
            WiredPlaceholderText.TriggerTextToken,
            out var text
        )
            ? text
            : NO_TRIGGER_TEXT;
}
