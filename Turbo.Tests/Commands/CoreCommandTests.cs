using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Permissions;
using Turbo.Rooms.Commands;
using Turbo.Rooms.Configuration;
using Xunit;

namespace Turbo.Tests.Commands;

public class CoreCommandTests
{
    private const int OWNER = 1;

    private readonly CommandRoomFixture _room = new();

    /// <summary>One category's item: a heading, then its commands on the lines under it.</summary>
    private static string Section(string category, params string[] commands) =>
        string.Join(CommandsCommand.LINE_BREAK, [$"---- {category} ----", .. commands]);

    public CoreCommandTests() =>
        _room.Commands.Register([
            new CommandsCommand(_room.Commands),
            new KickCommand(),
            new MuteCommand(Options.Create(_room.Config)),
            new UnloadRoomCommand(),
            new GuardedCommand(),
        ]);

    [Fact]
    public async Task CommandsListsOnlyWhatTheExecutorMayUse_InAScrollableNotice()
    {
        _room.AddPlayer(2, PermissionNodes.Command.COMMANDS, PermissionNodes.Command.KICK);

        await _room.SayAsync(2, ":commands");

        _room
            .NoticesTo(2)
            .Should()
            .ContainSingle()
            .Which.Should()
            .Equal(
                Section("GENERAL", ":commands [command] - List the commands you can use"),
                Section("MODERATION", ":kick <target> - Kick a player from the room")
            );
        _room.WhispersTo(2).Should().BeEmpty();
        _room.SaidToRoom().Should().BeEmpty();
    }

    [Fact]
    public async Task CommandsLeavesOutWhatNeedsMoreOfTheRoomThanTheExecutorHas()
    {
        _room.AddPlayer(2, PermissionNodes.Command.COMMANDS, "command.guarded");

        await _room.SayAsync(2, ":commands");
        _room.GiveRights(2);
        await _room.SayAsync(2, ":commands");

        var notices = _room.NoticesTo(2).ToList();
        notices[0]
            .Should()
            .Equal(Section("GENERAL", ":commands [command] - List the commands you can use"));
        notices[1]
            .Should()
            .Equal(
                Section(
                    "GENERAL",
                    ":commands [command] - List the commands you can use",
                    ":guarded"
                )
            );
    }

    [Fact]
    public async Task CommandsAreGroupedByCategory_GeneralFirst_ThenAlphabetical_AndSortedWithin()
    {
        _room.Commands.Register([new ProbeCommand()]);
        _room.AddPlayer(
            2,
            PermissionNodes.Command.COMMANDS,
            PermissionNodes.Command.MUTE,
            PermissionNodes.Command.KICK,
            "command.probe",
            "command.guarded"
        );
        _room.GiveRights(2);

        await _room.SayAsync(2, ":commands");

        _room
            .NoticesTo(2)
            .Should()
            .ContainSingle()
            .Which.Should()
            .Equal(
                Section(
                    "GENERAL",
                    ":commands [command] - List the commands you can use",
                    ":guarded"
                ),
                Section(
                    "MODERATION",
                    ":kick <target> - Kick a player from the room",
                    ":mute <target> [minutes] - Mute a player in the room"
                ),
                Section("ROLEPLAY", ":probe [text] - Test probe")
            );
    }

    [Fact]
    public async Task TheOwnerCanKickSomeone_WhoIsRemovedAndHasTheirSessionClosed()
    {
        _room.AddPlayer(OWNER, PermissionNodes.Command.KICK);
        _room.AddPlayer(3);

        await _room.SayAsync(OWNER, ":kick PLAYER3");

        _room.IsInRoom(3).Should().BeFalse();
        _room.SaidToRoom().Should().BeEmpty();
        _room.WhispersTo(OWNER).Should().Equal("player3 was kicked from the room.");
        _room.Harness.Fakes.Log.On<IPlayerNoticeService>().Should().BeEmpty();
        _room
            .Harness.Fakes.Log.Of("OnRemovedFromRoomAsync")
            .Should()
            .ContainSingle(x => (bool)x.Args[1]!);
    }

    [Fact]
    public async Task AFailedMuteNoticeDoesNotUndoTheMuteOrChangeItsSuccessReply()
    {
        _room.Harness.Fakes.Handlers[nameof(IPlayerNoticeService.SendAsync)] = _ =>
            PlayerNoticeDelivery.Failed;
        _room.AddPlayer(OWNER, PermissionNodes.Command.MUTE);
        _room.AddPlayer(3);

        await _room.SayAsync(OWNER, ":mute player3 5");

        _room
            .MutedUntil(3)
            .Should()
            .BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(10));
        _room
            .WhispersTo(OWNER)
            .Should()
            .Equal(
                "player3 is muted for 5 minutes.",
                "The action result is unchanged, but delivery of 1 target notice(s) could not be confirmed."
            );
    }

    [Fact]
    public async Task KickingSomeoneWhoIsNotThere_NamesWhoWasNotFound()
    {
        _room.AddPlayer(OWNER, PermissionNodes.Command.KICK);

        await _room.SayAsync(OWNER, ":kick ghost");

        _room.WhispersTo(OWNER).Should().Equal("There is nobody called ghost in this room.");
    }

    [Fact]
    public async Task TheRoomsOwnRulesStillDecide_SoSomeoneWithoutThePowerIsRefused()
    {
        _room.AddPlayer(2, PermissionNodes.Command.KICK);
        _room.GiveRights(2);
        _room.AddPlayer(3);

        await _room.SayAsync(2, ":kick player3");

        _room.IsInRoom(3).Should().BeTrue();
        _room.WhispersTo(2).Should().Equal("You can't kick player3.");
    }

    [Fact]
    public async Task TheOwnerIsProtectedFromBeingKicked()
    {
        _room.AddPlayer(OWNER);
        _room.AddPlayer(2, PermissionNodes.Command.KICK);
        _room.GiveRights(2);

        await _room.SayAsync(2, ":kick player1");

        _room.IsInRoom(OWNER).Should().BeTrue();
    }

    [Fact]
    public async Task MuteTakesMinutes_AndTellsWhoWasMuted()
    {
        _room.AddPlayer(OWNER, PermissionNodes.Command.MUTE);
        _room.AddPlayer(3);

        await _room.SayAsync(OWNER, ":mute player3 5");

        _room
            .MutedUntil(3)
            .Should()
            .BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(10));
        _room.WhispersTo(OWNER).Should().Equal("player3 is muted for 5 minutes.");
        _room
            .Harness.Fakes.Log.On<IPlayerNoticeService>()
            .Should()
            .ContainSingle(x =>
                (PlayerId)x.Args[0]! == (PlayerId)3
                && (string)x.Args[1]! == "command.mute.target"
                && ((IReadOnlyList<string>)x.Args[3]!).Should().ContainSingle().Which == "5"
            );
    }

    [Fact]
    public async Task MuteCapsTheAppliedAndReportedDuration()
    {
        var config = new RoomConfig { ChatMuteMaxDurationMinutes = 3 };
        var room = new CommandRoomFixture(config);
        room.Commands.Register([new MuteCommand(Options.Create(config))]);
        room.AddPlayer(OWNER, PermissionNodes.Command.MUTE);
        room.AddPlayer(3);

        await room.SayAsync(OWNER, ":mute player3 10");

        room.MutedUntil(3)
            .Should()
            .BeCloseTo(DateTime.UtcNow.AddMinutes(3), TimeSpan.FromSeconds(10));
        room.WhispersTo(OWNER).Should().Equal("player3 is muted for 3 minutes.");
        room.Harness.Fakes.Log.On<IPlayerNoticeService>()
            .Should()
            .ContainSingle(x => ((IReadOnlyList<string>)x.Args[3]!).Single() == "3");
    }

    [Fact]
    public async Task UnloadRepliesThroughPlayerNoticeAfterRemovingTheExecutor()
    {
        _room.AddPlayer(OWNER, PermissionNodes.Command.UNLOADROOM);

        await _room.SayAsync(OWNER, ":unloadroom");

        _room.WhispersTo(OWNER).Should().BeEmpty();
        _room
            .Harness.Fakes.Log.On<IPlayerNoticeService>()
            .Should()
            .ContainSingle(x =>
                (PlayerId)x.Args[0]! == (PlayerId)OWNER
                && (string)x.Args[1]! == "command.unloadroom.unloaded"
            );
    }

    [Fact]
    public async Task MuteWithoutMinutesUsesTheConfiguredDefault()
    {
        _room.AddPlayer(OWNER, PermissionNodes.Command.MUTE);
        _room.AddPlayer(3);

        await _room.SayAsync(OWNER, ":mute player3");

        _room
            .MutedUntil(3)
            .Should()
            .BeCloseTo(
                DateTime.UtcNow.AddMinutes(new RoomConfig().CommandDefaultMuteMinutes),
                TimeSpan.FromSeconds(10)
            );
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task AMuteOfNoTimeIsRefused(string minutes)
    {
        _room.AddPlayer(OWNER, PermissionNodes.Command.MUTE);
        _room.AddPlayer(3);

        await _room.SayAsync(OWNER, $":mute player3 {minutes}");

        _room.MutedUntil(3).Should().BeNull();
        _room.WhispersTo(OWNER).Should().Equal("You can't mute player3.");
    }

    [Fact]
    public void EveryCoreCommandIsGrantedByDefault_SoANewPlayerCanUseThem()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        foreach (
            var node in new[]
            {
                PermissionNodes.Command.COMMANDS,
                PermissionNodes.Command.KICK,
                PermissionNodes.Command.MUTE,
            }
        )
            registry.Nodes[node].GrantedByDefault.Should().BeTrue();
    }
}
