using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The official client writes "n" for an empty variable slot (VariablePickerPreset and
/// ValueOrVariablePopupPreset.finalizeSelection return WiredVariable.NONE for no selection), and
/// Habbo keeps the slot and sends it back as "0" (official client capture, WIN63-202610121709:
/// Variable Age saved ["n","n"] reopens ["0","0"]; a value-mode Give Variable ["303135","n"]
/// reopens ["303135","0"]). Ours dropped "n", so a later variable slid into the empty slot and the
/// editor got one id fewer back.
/// </summary>
public sealed class WiredEmptyVariableSlotTests
{
    private const int BOX = 2;
    private const int LEVEL_BOX = 10;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_empty_first_slot_does_not_take_the_reference()
    {
        var level = await LevelAsync();
        var age = _room.AddBox<WiredConditionVariableAgeMatch>(BOX, 0, 0, "wf_cnd_var_age_match");

        // No variable to age, the duration read from "level".
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [(int)WiredVariableTargetType.Furni, 0, 0, 0, 0, 1, 1, 0],
                definitionSpecifics: [0],
                variableIds: ["n", level]
            )
        )
            .Should()
            .BeTrue();

        Sent(age).Should().Equal("0", level);
    }

    [Fact]
    public async Task A_value_mode_give_variable_keeps_its_empty_reference()
    {
        var level = await LevelAsync();
        var give = _room.AddBox<WiredActionGiveVariable>(BOX, 0, 0, "wf_act_give_var");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [(int)WiredVariableTargetType.Furni, 0, 0, 0, 0, -10, 0],
                definitionSpecifics: [0],
                variableIds: [level, "n"]
            )
        )
            .Should()
            .BeTrue();

        Sent(give).Should().Equal(level, "0");
    }

    private async Task<string> LevelAsync()
    {
        var level = _room.AddBox<WiredVariableFurni>(LEVEL_BOX, 4, 4, "wf_var_furni");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                LEVEL_BOX,
                intParams: [(int)WiredAvailabilityType.RoomActive, 1],
                stringParam: "level"
            )
        )
            .Should()
            .BeTrue();
        await level.LoadWiredAsync(Ct);
        await _room
            .Harness.Module<Turbo.Rooms.Grains.Systems.RoomWiredSystem>()
            .OnRoomEventAsync(
                new Turbo.Primitives.Rooms.Events.Wired.WiredVariableBoxChangedEvent
                {
                    RoomId = 1,
                    CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                    BoxIds = [LEVEL_BOX],
                },
                Ct
            );
        await _room
            .Harness.Module<Turbo.Rooms.Grains.Systems.RoomWiredSystem>()
            .ProcessWiredAsync(11_000, dormant: false, Ct);

        return level.GetVarSnapshot().VariableId.ToString();
    }

    /// <summary>
    /// The variable ids the editor is sent when the box opens: the snapshot's, and checked in the
    /// bytes (the id list as the client reads it, int count then strings).
    /// </summary>
    private static List<string> Sent(FurnitureWiredLogic box)
    {
        var snapshot = box.GetSnapshot();
        var ids = snapshot.VariableIds.Select(id => id.ToString()).ToList();
        IComposer composer =
            snapshot.WiredType == WiredType.Condition
                ? new WiredFurniConditionEventMessageComposer { WiredData = snapshot }
                : new WiredFurniActionEventMessageComposer { WiredData = snapshot };
        var packet = PacketHarness.Encode(composer);
        var bytes = packet.PopBytes(packet.Remaining);
        var expected = new List<byte>();

        expected.AddRange(BigEndian(ids.Count));

        foreach (var id in ids)
        {
            var text = System.Text.Encoding.UTF8.GetBytes(id);

            expected.Add((byte)(text.Length >> 8));
            expected.Add((byte)text.Length);
            expected.AddRange(text);
        }

        bytes
            .AsSpan()
            .IndexOf(expected.ToArray())
            .Should()
            .BeGreaterThan(0, "the ids go out as they are stored");

        return ids;
    }

    private static byte[] BigEndian(int value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
