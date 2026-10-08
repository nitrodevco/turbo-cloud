using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.User;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The Wired Faculty tutorial "Example for WIRED Variable: Echo to show the user's direction"
/// (06/03/2025): an Echo of <c>@direction</c> named "user_direction" with a Text Connector on it
/// ("3=South" and so on), read by a Variable placeholder set to show text, so a message says
/// "You are looking at the South.". The Echo only passed on its source's labels and ignored the
/// Text Connector stacked on it.
/// </summary>
public sealed class WiredEchoTextConnectorTests
{
    private const int PLAYER_INDEX = 5;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_echo_shows_the_labels_of_the_text_connector_on_it()
    {
        var avatar = _room.Enter(PLAYER_INDEX, 1, 1);

        // The tutorial labels the room's isometric directions: rotation 3 is "South".
        avatar.SetRotation((Rotation)3);

        var direction = new UserDirectionVariable(_room.Harness.Room);
        var directionId = direction.GetVarSnapshot().VariableId;

        ((IDictionary)RoomHarness.GetMember(_room.Harness.Room.WiredSystem, "_variableById")!)[
            directionId
        ] = direction;

        var echo = _room.AddBox<WiredVariableEcho>(10, 6, 6, "wf_var_echo");
        _room.AddBox<WiredAddonVariableTextConnector>(11, 6, 6, "wf_xtra_var_text_connector");

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                11,
                stringParam: "0=North-East\n1=East\n2=South-East\n3=South\n4=South-West\n5=West\n6=North-West\n7=North"
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                stringParam: "user_direction",
                variableIds: [directionId.ToString()]
            )
        )
            .Should()
            .BeTrue();
        await echo.LoadWiredAsync(Ct);
        await _room
            .Harness.Module<RoomWiredSystem>()
            .OnRoomEventAsync(
                new RoomWiredStackChangedEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    StackIds = [_room.Map.ToIdx(6, 6)],
                },
                Ct
            );
        await _room.Harness.Module<RoomWiredSystem>().ProcessWiredAsync(11_000, dormant: false, Ct);

        echo.GetVarSnapshot().TextConnectors.Should().ContainKey(3).WhoseValue.Should().Be("South");

        var placeholder = _room.AddBox<WiredAddonVariablePlaceholder>(
            1,
            0,
            0,
            "wf_xtra_text_output_variable"
        );

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                1,
                intParams: [0, (int)WiredVariableTargetType.User, 1],
                stringParam: "user_direction",
                variableIds: [echo.GetVarSnapshot().VariableId.ToString()]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);
        ctx.Policy.TextPlaceholders.Add(placeholder);

        (await ctx.FormatTextAsync("You are looking at the $(user_direction).", Ct))
            .Should()
            .Be("You are looking at the South.");
    }
}
