using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Variables.Context;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// WIN63-202610121624 reads two more things in every wired variable (AS3
/// <c>wiredcontext/variables/WiredVariable.as</c>, order from its p-code): a
/// <c>requiresExecutionContext</c> bool after <c>isInvisible</c>, and, inside the text connector
/// block after the value/text pairs, <c>bool hasFallback, [string fallback]</c>. The fallback is a
/// text connector's <c>_=</c> line (Wired Faculty, 12/10/2026): <c>Util.getConnectedText</c> shows
/// it for a value without a label of its own, and the server's placeholder does the same.
/// </summary>
public sealed class WiredTextConnectorFallbackTests
{
    private const int PLAYER_INDEX = 5;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_value_without_a_label_shows_the_fallback_line()
    {
        var variable = await UserVariableWithConnectorAsync("1=One\n_=Some other number");

        variable.GetVarSnapshot().TextConnectorFallback.Should().Be("Some other number");
        variable.GetVarSnapshot().TextConnectors.Should().ContainSingle();

        await variable.GiveValueAsync(
            new WiredVariableKey(
                variable.GetVarSnapshot().VariableId,
                WiredVariableTargetType.User,
                PLAYER_INDEX
            ),
            new WiredVariableValue(7)
        );

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
                stringParam: "points",
                variableIds: [variable.GetVarSnapshot().VariableId.ToString()]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);
        ctx.Policy.TextPlaceholders.Add(placeholder);

        (await ctx.FormatTextAsync("You have $(points).", Ct))
            .Should()
            .Be("You have Some other number.");
    }

    [Fact]
    public async Task The_client_reads_the_fallback_after_the_pairs()
    {
        var variable = await UserVariableWithConnectorAsync("1=One\n_=Other");
        var packet = Encode(variable.GetVarSnapshot());

        ReadUpToTextConnector(packet).Should().BeFalse("a created user variable has a holder");
        packet.PopBoolean().Should().BeTrue("hasTextConnector");
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be("One");
        packet.PopBoolean().Should().BeTrue("hasFallback");
        packet.PopString().Should().Be("Other");
        packet.End.Should().BeTrue();
    }

    [Fact]
    public async Task A_fallback_alone_still_sends_the_text_connector()
    {
        var variable = await UserVariableWithConnectorAsync("_=Other");
        var packet = Encode(variable.GetVarSnapshot());

        ReadUpToTextConnector(packet);
        packet.PopBoolean().Should().BeTrue("the client only reads a fallback inside the block");
        packet.PopInt().Should().Be(0);
        packet.PopBoolean().Should().BeTrue();
        packet.PopString().Should().Be("Other");
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void A_context_variable_requires_an_execution_context()
    {
        var packet = Encode(
            new ContextSignalUserCountVariable(_room.Harness.Room).GetVarSnapshot()
        );

        ReadUpToTextConnector(packet).Should().BeTrue();
    }

    private async Task<WiredVariableUser> UserVariableWithConnectorAsync(string connectorText)
    {
        _room.Enter(PLAYER_INDEX, 1, 1);

        var variable = _room.AddBox<WiredVariableUser>(10, 6, 6, "wf_var_user");
        _room.AddBox<WiredAddonVariableTextConnector>(11, 6, 6, "wf_xtra_var_text_connector");

        (await _room.SaveAsync<UpdateAddonMessage>(11, stringParam: connectorText))
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: "points"
            )
        )
            .Should()
            .BeTrue();
        await variable.LoadWiredAsync(Ct);
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
        await _room.Harness.Module<RoomWiredSystem>().ProcessWiredAsync(10_000, dormant: false, Ct);

        return variable;
    }

    /// <summary>The variable as the diffs message carries it, past the message's own header.</summary>
    private static Turbo.Primitives.Packets.ClientPacket Encode(WiredVariableSnapshot snapshot)
    {
        var packet = PacketHarness.Encode(
            new WiredAllVariablesDiffsEventMessageComposer
            {
                AllVariablesHash = new WiredVariableHash(0),
                IsLastChunk = true,
                RemovedVariableIds = [],
                AddedOrUpdated = [snapshot],
            }
        );

        packet.PopInt();
        packet.PopBoolean();
        packet.PopInt();
        packet.PopInt();
        packet.PopInt();

        return packet;
    }

    /// <summary>
    /// Reads a variable up to its text connector flag, in <c>WiredVariable</c>'s order, and returns
    /// <c>requiresExecutionContext</c>.
    /// </summary>
    private static bool ReadUpToTextConnector(Turbo.Primitives.Packets.ClientPacket packet)
    {
        packet.PopString();
        packet.PopInt();
        packet.PopString();
        packet.PopInt();
        packet.PopInt();

        for (var i = 0; i < 6; i++)
            packet.PopBoolean();

        var requiresExecutionContext = packet.PopBoolean();

        packet.PopBoolean();
        packet.PopBoolean();

        return requiresExecutionContext;
    }
}
