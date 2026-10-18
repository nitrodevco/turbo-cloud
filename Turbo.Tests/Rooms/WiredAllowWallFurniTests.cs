using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The editor's <c>allowWallFurni</c> is the box's own: Habbo lets only the boxes that work on
/// any furni's state, position, type or variables pick wall furni, and refuses it to every other
/// box (official client captures of the box editors, WIN63-202610121709 to 202610151745: every
/// recorded box agrees, true on e.g. Change Variable, Furni With Variable, State Changed, false on
/// e.g. Chase, Furni Have Avatars, Walks On). Ours sent true on every box, so wall items could be
/// picked into Chase, Move To Direction, Teleport To and the like.
/// </summary>
public sealed class WiredAllowWallFurniTests
{
    private const int FLOOR = 50;
    private const int WALL = 51;

    /// <summary>(type, code) of every box Habbo lets pick wall furni.</summary>
    private static readonly HashSet<(WiredType, int)> Official =
    [
        .. new[] { 0, 3, 4, 15, 29, 33, 34, 39, 40, 41, 45, 46, 47, 55, 57 }.Select(c =>
            (WiredType.Action, c)
        ),
        .. new[] { 15, 18, 19, 20 }.Select(c => (WiredType.Addon, c)),
        .. new[] { 0, 8, 36, 40, 42, 43, 45, 46 }.Select(c => (WiredType.Condition, c)),
        // The negative States Match, Furni Type Matches and Has Variable use their positive's
        // editor (inference: unrecorded; the recorded negatives match their positives).
        .. new[] { 13, 19, 41 }.Select(c => (WiredType.Condition, c)),
        .. new[] { 0, 1, 17, 18 }.Select(c => (WiredType.Selector, c)),
        .. new[] { 4, 18, 20 }.Select(c => (WiredType.Trigger, c)),
    ];

    private readonly WiredRoom _room = new(8, 8);

    public static TheoryData<Type> Boxes()
    {
        var data = new TheoryData<Type>();

        foreach (
            var type in typeof(FurnitureWiredLogic)
                .Assembly.GetTypes()
                .Where(t =>
                    !t.IsAbstract
                    && t.IsSubclassOf(typeof(FurnitureWiredLogic))
                    && t.GetCustomAttribute<RoomObjectLogicAttribute>() is not null
                )
                .OrderBy(t => t.Name)
        )
            data.Add(type);

        return data;
    }

    [Theory]
    [MemberData(nameof(Boxes))]
    public async Task Each_box_lets_its_editor_pick_wall_furni_as_Habbos_does(Type type)
    {
        var box = AddBox(type, 1);

        await box.LoadWiredAsync(TestContext.Current.CancellationToken);

        box.GetSnapshot()
            .AllowWallFurni.Should()
            .Be(
                Official.Contains((box.WiredType, box.WiredCode)),
                $"{type.Name} is {box.WiredType} {box.WiredCode}"
            );
    }

    [Fact]
    public async Task A_box_that_may_not_pick_wall_furni_drops_one_from_its_save()
    {
        AddBox(typeof(WiredActionChaseHabbo), 1);
        _room.AddFloorItem(FLOOR, 4, 4);
        _room.AddWallItem(WALL);

        (await _room.SaveAsync<UpdateActionMessage>(1, stuffIds: [-WALL, FLOOR])).Should().BeTrue();

        ((FurnitureWiredLogic)_room.FloorItem(1).Logic)
            .GetSnapshot()
            .StuffIds.Should()
            .Equal(FLOOR);
    }

    [Fact]
    public async Task A_box_that_may_pick_wall_furni_keeps_one()
    {
        AddBox(typeof(WiredActionToggleItemState), 1);
        _room.AddFloorItem(FLOOR, 4, 4);
        _room.AddWallItem(WALL);

        (await _room.SaveAsync<UpdateActionMessage>(1, intParams: [0], stuffIds: [-WALL, FLOOR]))
            .Should()
            .BeTrue();

        ((FurnitureWiredLogic)_room.FloorItem(1).Logic)
            .GetSnapshot()
            .StuffIds.Should()
            .Equal(-WALL, FLOOR);
    }

    private FurnitureWiredLogic AddBox(Type type, int id)
    {
        FurnitureWiredLogic? box = null;

        _room.AddFloorItem(
            id,
            1,
            1,
            type.GetCustomAttribute<RoomObjectLogicAttribute>()!.Key,
            createLogic: (factory, ctx) =>
                box = (FurnitureWiredLogic)
                    Activator.CreateInstance(type, _room.Grains, factory, ctx)!
        );

        return box!;
    }
}
