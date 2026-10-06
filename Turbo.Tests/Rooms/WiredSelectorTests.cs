using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The selectors a hotel reaches for first, saved the way the client's editor saves them and run
/// against a real room: Furni In Area (the rectangle the editor drags out), Users In Area, Furni
/// By Type, and the "walks on furni" trigger that starts the stack.
/// </summary>
public sealed class WiredSelectorTests
{
    private const int BOX = 7;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<IWiredSelectionSet> SelectAsync(
        IWiredSelector selector,
        IWiredSelectionSet? signal = null,
        IWiredSelectionSet? triggered = null
    ) =>
        selector.SelectAsync(
            new WiredProcessingContext(_room.Harness.Room)
            {
                Event = new AvatarWalkOnFurniEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    ObjectId = 0,
                    FurniId = 0,
                },
                Stack = _room.Harness.Fakes.Create<IWiredStack>(),
                Signal = signal ?? new WiredSelectionSet(),
                Selected = triggered ?? new WiredSelectionSet(),
                CancellationToken = Ct,
            },
            Ct
        );

    // --- Furni In Area ---

    [Fact]
    public async Task FurniInArea_PicksTheFurniInsideTheRectangleTheEditorSent()
    {
        var inside = new[] { 10, 11, 12 };
        _room.AddFloorItem(10, 3, 3);
        _room.AddFloorItem(11, 4, 3);
        _room.AddFloorItem(12, 3, 4);
        _room.AddFloorItem(13, 6, 6);
        _room.AddFloorItem(14, 1, 1);
        var box = _room.AddBox<WiredSelectorItemsInArea>(BOX, 0, 0, "wf_slc_furni_area");

        // Root x, root y, width, height: what `InArea.readIntParamsFromForm` sends.
        (await _room.SaveAsync<UpdateSelectorMessage>(BOX, intParams: [3, 3, 2, 2]))
            .Should()
            .BeTrue();

        (await SelectAsync(box)).SelectedFurniIds.Should().BeEquivalentTo(inside);
    }

    [Fact]
    public async Task FurniInArea_ARectangleThatWasClearedPicksNothing()
    {
        _room.AddFloorItem(10, 3, 3);
        var box = _room.AddBox<WiredSelectorItemsInArea>(BOX, 0, 0, "wf_slc_furni_area");

        (await _room.SaveAsync<UpdateSelectorMessage>(BOX, intParams: [0, 0, 0, 0]))
            .Should()
            .BeTrue();

        (await SelectAsync(box)).SelectedFurniIds.Should().BeEmpty();
    }

    [Fact]
    public async Task FurniInArea_ARectangleOverTheEdgeOfTheRoomIsCutToIt()
    {
        _room.AddFloorItem(10, 7, 7);
        _room.AddFloorItem(11, 2, 2);
        var box = _room.AddBox<WiredSelectorItemsInArea>(BOX, 0, 0, "wf_slc_furni_area");

        (await _room.SaveAsync<UpdateSelectorMessage>(BOX, intParams: [6, 6, 10, 10]))
            .Should()
            .BeTrue();

        (await SelectAsync(box)).SelectedFurniIds.Should().BeEquivalentTo([10]);
    }

    [Fact]
    public async Task FurniInArea_AOneTileRectangleFromAShiftClickPicksThatTile()
    {
        _room.AddFloorItem(10, 5, 2);
        _room.AddFloorItem(11, 5, 3);
        var box = _room.AddBox<WiredSelectorItemsInArea>(BOX, 0, 0, "wf_slc_furni_area");

        (await _room.SaveAsync<UpdateSelectorMessage>(BOX, intParams: [5, 2, 1, 1]))
            .Should()
            .BeTrue();

        (await SelectAsync(box)).SelectedFurniIds.Should().BeEquivalentTo([10]);
    }

    // --- Users In Area ---

    [Fact]
    public async Task UsersInArea_PicksTheUsersStandingInTheRectangle()
    {
        _room.Enter(5, 3, 3);
        _room.Enter(6, 4, 4);
        _room.Enter(7, 1, 1);
        var box = _room.AddBox<WiredSelectorEntitiesInArea>(BOX, 0, 0, "wf_slc_users_area");

        (await _room.SaveAsync<UpdateSelectorMessage>(BOX, intParams: [3, 3, 2, 2]))
            .Should()
            .BeTrue();

        (await SelectAsync(box))
            .SelectedAvatarIds.Should()
            .BeEquivalentTo([(RoomObjectId)5, (RoomObjectId)6]);
    }

    // --- Furni By Type ---

    [Fact]
    public async Task FurniByType_PicksEveryFurniOfTheKindsThePickedOnesAre()
    {
        // 10, 11 and 13 are one kind of furni; 12 is another.
        _room.AddFloorItem(10, 1, 1, definitionId: 77);
        _room.AddFloorItem(11, 2, 1, definitionId: 77);
        _room.AddFloorItem(12, 3, 1, definitionId: 78);
        _room.AddFloorItem(13, 6, 6, definitionId: 77);
        var box = _room.AddBox<WiredSelectorItemsByType>(BOX, 0, 0, "wf_slc_furni_bytype");

        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                BOX,
                intParams: [0],
                stuffIds: [10],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        (await SelectAsync(box)).SelectedFurniIds.Should().BeEquivalentTo([10, 11, 13]);
    }

    [Fact]
    public async Task FurniByType_StateMatchOff_IgnoresWhatStateTheyAreIn()
    {
        var gates = await PlaceGatesAsync(stateMatch: false);

        (await SelectAsync(gates)).SelectedFurniIds.Should().BeEquivalentTo([10, 11, 12]);
    }

    [Fact]
    public async Task FurniByType_StateMatchOn_PicksOnlyFurniInTheSameStateAsAPickedOne()
    {
        var gates = await PlaceGatesAsync(stateMatch: true);

        // 10 is the picked one and is open (state 1); 12 is open as well; 11 is closed.
        (await SelectAsync(gates))
            .SelectedFurniIds.Should()
            .BeEquivalentTo([10, 12]);
    }

    /// <summary>Three furni of one kind: 10 and 12 open, 11 closed. 10 is the one picked.</summary>
    private async Task<WiredSelectorItemsByType> PlaceGatesAsync(bool stateMatch)
    {
        foreach (var (id, x, state) in new[] { (10, 1, 1), (11, 2, 0), (12, 3, 1) })
        {
            var item = _room.AddFloorItem(id, x, 1, definitionId: 77);

            await item.Logic.SetStateAsync(state, false);
        }

        var box = _room.AddBox<WiredSelectorItemsByType>(BOX, 0, 0, "wf_slc_furni_bytype");

        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                BOX,
                intParams: [stateMatch ? 1 : 0],
                stuffIds: [10],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        return box;
    }

    [Fact]
    public async Task FurniByType_PicksTheKindsOfTheFurniASignalCarried()
    {
        _room.AddFloorItem(10, 1, 1, definitionId: 77);
        _room.AddFloorItem(12, 3, 1, definitionId: 78);
        _room.AddFloorItem(14, 4, 1, definitionId: 78);
        var box = _room.AddBox<WiredSelectorItemsByType>(BOX, 0, 0, "wf_slc_furni_bytype");

        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                BOX,
                intParams: [0],
                furniSources:
                [
                    [WiredFurniSourceType.SignalItems],
                ]
            )
        ).Should().BeTrue();

        (await SelectAsync(box, signal: new WiredSelectionSet([12], [])))
            .SelectedFurniIds.Should()
            .BeEquivalentTo([12, 14]);
    }

    [Fact]
    public async Task FurniByType_PicksTheKindsOfTheFurniThatTriggeredTheStack()
    {
        _room.AddFloorItem(10, 1, 1, definitionId: 77);
        _room.AddFloorItem(11, 2, 1, definitionId: 77);
        _room.AddFloorItem(12, 3, 1, definitionId: 78);
        var box = _room.AddBox<WiredSelectorItemsByType>(BOX, 0, 0, "wf_slc_furni_bytype");

        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                BOX,
                intParams: [0],
                furniSources:
                [
                    [WiredFurniSourceType.TriggeredItem],
                ]
            )
        ).Should().BeTrue();

        (await SelectAsync(box, triggered: new WiredSelectionSet([11], [])))
            .SelectedFurniIds.Should()
            .BeEquivalentTo([10, 11]);
    }

    [Fact]
    public async Task FurniByType_WithNothingPicked_PicksNothing()
    {
        _room.AddFloorItem(10, 1, 1, definitionId: 77);
        var box = _room.AddBox<WiredSelectorItemsByType>(BOX, 0, 0, "wf_slc_furni_bytype");

        (await _room.SaveAsync<UpdateSelectorMessage>(BOX, intParams: [0])).Should().BeTrue();

        (await SelectAsync(box)).SelectedFurniIds.Should().BeEmpty();
    }

    // --- Walks On Furni ---

    [Fact]
    public async Task WalksOnFurni_FiresForAUserWalkingOntoAPickedFurni_AndNotForAnother()
    {
        var heard = new List<RoomEvent>();

        _room.Harness.EventListeners.Register([new Recorder(heard)]);
        _room.AddFloorItem(20, 5, 5);
        _room.AddFloorItem(21, 6, 6);
        _room.Enter(5, 1, 1);
        var trigger = _room.AddBox<WiredTriggerWalkOnFurni>(BOX, 0, 0, "wf_trg_walks_on_furni");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                BOX,
                stuffIds: [20],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        // The room tells the furni an avatar arrived; the furni says so to the room.
        await _room
            .Harness.Module<Turbo.Rooms.Grains.Modules.RoomAvatarModule>()
            .RelocateAvatarAsync(_room.Avatars[5], _room.Map.ToIdx(5, 5), Ct);
        await _room
            .Harness.Module<Turbo.Rooms.Grains.Modules.RoomAvatarModule>()
            .RelocateAvatarAsync(_room.Avatars[5], _room.Map.ToIdx(6, 6), Ct);

        var walked = heard.OfType<AvatarWalkOnFurniEvent>().ToList();

        walked.Select(e => e.FurniId.Value).Should().Equal(20, 21);
        (await trigger.MatchesEventAsync(walked[0], Ct)).Should().BeTrue("furni 20 is picked");
        (await trigger.MatchesEventAsync(walked[1], Ct)).Should().BeFalse("furni 21 is not");
        walked[0].ObjectId.Value.Should().Be(5, "the event names the user who walked on");
    }

    private sealed class Recorder(List<RoomEvent> heard) : IRoomEventListener
    {
        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            heard.Add(evt);

            return Task.CompletedTask;
        }
    }
}
