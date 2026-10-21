using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Link;
using Turbo.Rooms.Wired.Link;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// WIRED Link Speaker (<c>wf_xtra_link_speaker</c>, add-on code 2003): says what arrives through
/// the WIRED Links it picked, in the speaker bubble (chat style 253, <c>wired_link_speaker</c>)
/// drawn at its own tile. It needs no stack: alone on a tile it speaks for its Links, and blinks
/// as it does (official client capture 2026-10-21, 3 of 3). Params as the AS3 editor writes them
/// (WIN63-202610201032 <c>_-z14.as</c>, "Speak for:"): 0 Message, 1 Dataframe, each 1 when
/// ticked; a fresh box has both (recorded [1,1]). A dataframe is spoken as its rows, one per line,
/// the key in bold: <c>[b]score[/b]: 42</c> (recorded).
/// </summary>
[RoomObjectLogic("wf_xtra_link_speaker")]
public class WiredAddonLinkSpeaker(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    /// <summary>The chat style of the bubble (Habbo's chatstyles: id 253, asset <c>wired_link_speaker</c>).</summary>
    public const int SPEAKER_CHAT_STYLE = 253;

    public override int WiredCode => (int)WiredAddonType.LINK_SPEAKER;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredBoolParamRule(true), new WiredBoolParamRule(true)];

    // Its picks are the Links it speaks for; the editor offers no source for them (recorded:
    // no furni source, AmountFurniSelections 0, the Links in stuffIds).
    protected override bool CanPickFurni(IRoomItem item) => WiredLinkBoxes.IsLink(item);

    protected override string? PickRefusedErrorKey => WiredLinkBoxes.REQUIRE_LINK_FURNI;

    /// <summary>
    /// Says <paramref name="payload"/> when it arrived through one of this speaker's Links and
    /// the speaker speaks for its kind. Each viewer's bubble names them as its receiver
    /// (recorded: the receiver room index is the viewer's own).
    /// </summary>
    public async Task<bool> SpeakAsync(
        ImmutableArray<int> linkIds,
        WiredLinkPayload payload,
        CancellationToken ct
    )
    {
        if (
            !GetIntParamOrDefault(payload.IsDataframe ? 1 : 0, true)
            || !GetStuffIds().Any(linkIds.Contains)
        )
            return false;

        var text = payload.IsDataframe
            ? string.Join("\n", payload.Rows.Select(row => $"[b]{row.Key}[/b]: {row.Value}"))
            : payload.Message ?? string.Empty;

        if (text.Length == 0)
            return false;

        var location = new ChatBubbleLocation(_ctx.RoomObject.X, _ctx.RoomObject.Y);

        foreach (var viewer in AvatarModule.Players.ToList())
        {
            await _grainFactory.SendComposerToPlayerAsync(
                viewer.PlayerId,
                new ChatMessageComposer
                {
                    ObjectId = -1,
                    Text = text,
                    Gesture = AvatarGestureType.None,
                    StyleId = SPEAKER_CHAT_STYLE,
                    Links = [],
                    TrackingId = -1,
                    ReceiverRoomIndex = viewer.ObjectId.Value,
                    HideUsername = true,
                    Location = location,
                },
                ct
            );
        }

        await FlashActivationStateAsync(ct);

        return true;
    }
}
