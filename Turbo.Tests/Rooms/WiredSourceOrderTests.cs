using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The editor is sent each slot's allowed sources in ascending id order, whatever the default,
/// as Habbo sends them (every official editor-open capture; bot follow
/// 2026-10-22T22-54-01-Open: [[0,100,200,201],[0,200,201]] defaults [100,0]; furni in
/// neighbourhood 22-51-19-click: [[0,100,201]] / [[0,201]] defaults [100] / [0]). The editor's
/// "next" arrow steps in that order (official: 100 -> 200 on the bots slot) and a merged
/// section switched to furni takes the first (official: furniSrc [0]).
/// </summary>
public sealed class WiredSourceOrderTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public async Task A_bot_box_sends_its_slots_in_id_order()
    {
        var box = _room.AddBox<WiredActionBotFollowAvatar>(1, 0, 0, "wf_act_bot_follow_avatar");

        (await _room.SaveAsync<UpdateActionMessage>(1, intParams: [1], stringParam: "Frank"))
            .Should()
            .BeTrue();

        // furni slots 0; user slots 2: [0,100,200,201], [0,200,201]; defaults: none, [100,0].
        Sent(new WiredFurniActionEventMessageComposer { WiredData = box.GetSnapshot() })
            .AsSpan()
            .IndexOf(Ints(0, 2, 4, 0, 100, 200, 201, 3, 0, 200, 201, 0, 2, 100, 0))
            .Should()
            .BeGreaterThan(0);
    }

    [Fact]
    public async Task A_neighbourhood_selector_sends_its_slots_in_id_order()
    {
        var box = _room.AddBox<WiredSelectorItemsInNeighborhood>(
            1,
            0,
            0,
            "wf_slc_furni_neighborhood"
        );

        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                1,
                intParams: [1, 0, 0, 33554431, .. new int[13]],
                definitionSpecifics: [false, false]
            )
        )
            .Should()
            .BeTrue();

        // furni slots 1: [0,100,201]; user slots 1: [0,201]; defaults [100] and [0].
        Sent(new WiredFurniSelectorEventMessageComposer { WiredData = box.GetSnapshot() })
            .AsSpan()
            .IndexOf(Ints(1, 3, 0, 100, 201, 1, 2, 0, 201, 1, 100, 1, 0))
            .Should()
            .BeGreaterThan(0);
    }

    private static byte[] Sent(IComposer composer)
    {
        var packet = PacketHarness.Encode(composer);

        return packet.PopBytes(packet.Remaining);
    }

    // The ints as the client reads them, big-endian, one run of bytes.
    private static byte[] Ints(params int[] values) =>
        [
            .. values.SelectMany(v =>
                new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }
            ),
        ];
}
