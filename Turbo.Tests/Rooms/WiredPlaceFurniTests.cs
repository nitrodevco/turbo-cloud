using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Place Temporary Furni" saved the way its editor saves it (<c>_-U1h</c>: custom target is a
/// user, location, altitude, offsets x / y / altitude, spawn with variable, value option, value,
/// value target): a click puts a copy of the lamp, as it stood when the box was saved, into the
/// room.
/// </summary>
public sealed class WiredPlaceFurniTests
{
    private const int CLICK_ME = 23;
    private const int LAMP = 20;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private RoomFurniModule Furni => _room.Harness.Module<RoomFurniModule>();

    [Theory]
    [InlineData(0, 0, 3, 3, false)]
    [InlineData(2, -1, 5, 2, false)]
    [InlineData(0, 0, 3, 3, true)]
    public async Task A_click_places_a_temporary_copy_of_the_lamp(
        int offsetX,
        int offsetY,
        int x,
        int y,
        bool snapshotWithoutType
    )
    {
        _room.Enter(5, 7, 7);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        var lamp = _room.AddFloorItem(LAMP, 3, 3);

        _room.Harness.Fakes.Handlers["TryGetDefinition"] = call =>
            call.Args[0] is int id && id == lamp.Definition.Id ? lamp.Definition : null;
        _room.Harness.Fakes.Handlers["CreateFloorItem"] = call =>
        {
            var item = new RoomFloorItem
            {
                ObjectId = (RoomObjectId)call.Args[0]!,
                OwnerId = (PlayerId)call.Args[1]!,
                OwnerName = string.Empty,
                Definition = (FurnitureDefinitionSnapshot)call.Args[2]!,
            };

            item.SetExtraData(null);

            return item;
        };

        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionPlaceFurni>(2, 0, 0, "wf_act_place_furni");

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
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [0, 0, 0, offsetX, offsetY, 0, 0, 0, 0, 0],
                stuffIds: [LAMP],
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        // A box saved before its snapshot kept each furni's type (before 20/09/2026).
        if (snapshotWithoutType)
            _room
                .FloorItem(2)
                .ExtraData.UpdateSection(
                    WiredFurniSnapshotEntry.SECTION,
                    new Dictionary<int, WiredFurniSnapshotEntry> { [LAMP] = new(0, 0, 3, 3, 0) }
                );

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

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(4);

        var copies = Furni.Items.Where(x => x.IsTemporary).ToList();

        copies.Should().ContainSingle();
        (copies[0].X, copies[0].Y, copies[0].Definition.Id).Should().Be((x, y, lamp.Definition.Id));
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
