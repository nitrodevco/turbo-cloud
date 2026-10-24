using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The random add-on's two sliders (AS3 1745 wired_setup/addons/_-D1I.as:30, params [skips,
/// picks]): "Pick %picks% effects" and "Avoid effects from last %skips% executions". A user clicks
/// a furni; the stack's two "teleport to" effects each send the clicker to their own furni.
/// </summary>
public sealed class WiredRandomAddonTests
{
    private const int CLICK_ME = 20;
    private const int LEFT = 21;
    private const int RIGHT = 22;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredRandomAddonTests()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(LEFT, 2, 6);
        _room.AddFloorItem(RIGHT, 6, 6);
        _room.Enter(5, 1, 1);
    }

    [Fact]
    public async Task AvoidingTheLastExecution_AlternatesBetweenTwoEffects()
    {
        await BuildAsync(avoid: 1, picks: 1);

        var landed = new List<string>();

        for (var i = 0; i < 6; i++)
        {
            await ClickAsync();
            landed.Add(_room.Positions()[0]);
        }

        // Whichever went first, the other runs next, every time.
        for (var i = 1; i < landed.Count; i++)
            landed[i]
                .Should()
                .NotBe(landed[i - 1], $"click {i + 1} avoids the effect of click {i}");
    }

    // Official client capture 2026-10-16 (Pick 1, Avoid last 2, two Toggle effects): 6 of 6 clicks
    // alternated. Ours avoided both once each had run, then picked either at random.
    [Fact]
    public async Task AvoidingMoreExecutionsThanThereAreOtherEffects_StillAlternates()
    {
        await BuildAsync(avoid: 2, picks: 1);

        var landed = new List<string>();

        for (var i = 0; i < 20; i++)
        {
            await ClickAsync();
            landed.Add(_room.Positions()[0]);
        }

        for (var i = 1; i < landed.Count; i++)
            landed[i]
                .Should()
                .NotBe(landed[i - 1], $"click {i + 1} avoids the effect of click {i}");
    }

    [Fact]
    public async Task AvoidingNothing_RunsOneOfTheEffects_NeitherIsLeftOut()
    {
        await BuildAsync(avoid: 0, picks: 1);

        var landed = new HashSet<string>();

        for (var i = 0; i < 40; i++)
        {
            await ClickAsync();
            landed.Add(_room.Positions()[0]);
        }

        landed.Should().BeEquivalentTo(["5@2,6", "5@6,6"]);
    }

    // addon.2: the unseen add-on runs each effect once before any repeats (confidential capture,
    // extras experiment: unseen_uses_each_effect_before_repeat, choices [1,2]).
    [Fact]
    public async Task Addon2_TheUnseenAddon_RunsEachEffectOnceBeforeRepeating()
    {
        await BuildAsync(avoid: 0, picks: 0, unseen: true);

        var landed = new List<string>();

        for (var i = 0; i < 6; i++)
        {
            await ClickAsync();
            landed.Add(_room.Positions()[0]);
        }

        for (var i = 0; i < landed.Count; i += 2)
            landed.Skip(i).Take(2).Should().BeEquivalentTo(["5@2,6", "5@6,6"]);
    }

    private async Task BuildAsync(int avoid, int picks, bool unseen = false)
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");

        if (unseen)
            _room.AddBox<WiredAddonUnseenActions>(2, 0, 0, "wf_xtra_unseen");
        else
            _room.AddBox<WiredAddonRandomActions>(2, 0, 0, "wf_xtra_random");
        _room.AddBox<WiredActionTeleportTo>(3, 0, 0, "wf_act_teleport_to");
        _room.AddBox<WiredActionTeleportTo>(4, 0, 0, "wf_act_teleport_to");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (await _room.SaveAsync<UpdateAddonMessage>(2, intParams: unseen ? [] : [avoid, picks]))
            .Should()
            .BeTrue();

        foreach (var (box, target) in new[] { (3, LEFT), (4, RIGHT) })
        {
            (
                await _room.SaveAsync<UpdateActionMessage>(
                    box,
                    intParams: [1],
                    stuffIds: [target],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                    playerSources:
                    [
                        [WiredPlayerSourceType.TriggeredUser],
                    ],
                    definitionSpecifics: [0]
                )
            ).Should().BeTrue();
        }

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
