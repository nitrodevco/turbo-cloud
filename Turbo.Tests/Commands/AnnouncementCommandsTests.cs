using FluentAssertions;
using Turbo.Commands;
using Turbo.Operations.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

public class AnnouncementCommandsTests : OperatorCommandsTestBase
{
    public AnnouncementCommandsTests()
    {
        var sessions = Hotel.Fakes.Create<Turbo.Primitives.Networking.ISessionGateway>();
        var texts = Hotel.Fakes.Create<Turbo.Primitives.Texts.IHotelTextProvider>();

        Hotel.Commands.Register([
            new WarnCommand(Grains, sessions, Config),
            new AlertCommand(Grains, sessions, Config),
            new RoomAlertCommand(Grains, Config),
            new HotelAlertCommand(Grains, sessions, Config),
            new EventAlertCommand(Grains, sessions, texts, Config),
        ]);

        Hotel.Fakes.Handlers["GetSummaryAsync"] = call =>
            call.Interface == typeof(Turbo.Primitives.Rooms.Grains.IRoomGrain)
                ? Task.FromResult(
                    new RoomSummarySnapshot
                    {
                        RoomId = 7,
                        Name = "Quiz night",
                        Description = "",
                        OwnerId = STAFF,
                        OwnerName = "staff",
                        Population = 3,
                        LastUpdatedUtc = START,
                    }
                )
                : Fakes.NotHandled;
    }

    [Fact]
    public async Task Alert_IsAPopUp_ForOneOnlinePlayer()
    {
        var staff = Staff(PermissionNodes.Command.ALERT);

        await Hotel.RunAsync("alert", staff, "alice  Please stop. ");

        SentTo(ALICE)
            .OfType<HabboBroadcastMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Be("Please stop.");
        staff.Replies.Should().Equal("Sent to 1 players.");
        SentTo(BOB).Should().BeEmpty();
    }

    [Theory]
    [InlineData("hotelalert", PermissionNodes.Command.HOTELALERT)]
    [InlineData("eventalert", PermissionNodes.Command.EVENTALERT)]
    public async Task Confirm_Broadcast_DoesNotIncludePlayersWhoJoinedAfterThePrompt(
        string command,
        string node
    )
    {
        Hotel.Commands.Register([new ConfirmCommand()]);
        for (var i = 100; i < 110; i++)
            Hotel.WithPlayer(i, $"p{i}");
        var staff = Staff(node);
        var outcome = await Hotel.RunAsync(command, staff, "hello");
        outcome.Should().Be(CommandOutcome.AwaitingConfirmation);

        Hotel.WithPlayer(200, "newcomer");
        await Hotel.RunAsync("confirm", staff, "");

        SentTo(200).Should().BeEmpty();
        SentTo(ALICE).Should().ContainSingle();
        staff.Replies[^1].Should().Be("Sent to 13 players.");
    }

    [Fact]
    public async Task Alert_ToSomeoneOffline_IsNotSent_AndSaysWhy()
    {
        var staff = Staff(PermissionNodes.Command.ALERT);

        await Hotel.RunAsync("alert", staff, "carol hello");

        SentTo(CAROL).Should().BeEmpty();
        staff.Replies.Should().Equal("Carol is not online.");
    }

    [Fact]
    public async Task Alert_NeedsAMessage()
    {
        var staff = Staff(PermissionNodes.Command.ALERT);

        await Hotel.RunAsync("alert", staff, "alice");

        staff.Replies.Should().Equal("Usage: :alert <who> <message>");
    }

    [Fact]
    public async Task Alert_RefusesAMessageOverTheLimit()
    {
        var staff = Staff(PermissionNodes.Command.ALERT);

        await Hotel.RunAsync(
            "alert",
            staff,
            "alice " + new string('x', Config.Value.MaxAlertLength + 1)
        );

        SentTo(ALICE).Should().BeEmpty();
        staff.Replies.Should().Equal("That message is too long. The most is 500 characters.");
    }

    [Fact]
    public async Task Alert_AtAGroup_NeedsTheMassNode_AndIsThenSentToEveryoneThere()
    {
        var staff = Staff(PermissionNodes.Command.ALERT);

        await Hotel.RunAsync("alert", staff, "@online hello");

        staff.Replies.Should().Equal("You can't use @online with that command.");
        SentTo(ALICE).Should().BeEmpty();

        var senior = Staff(PermissionNodes.Command.ALERT, PermissionNodes.Command.ALERT_MASS);

        await Hotel.RunAsync("alert", senior, "@room hello");

        SentTo(ALICE).OfType<HabboBroadcastMessageComposer>().Should().ContainSingle();
        SentTo(BOB).OfType<HabboBroadcastMessageComposer>().Should().ContainSingle();
        SentTo(STAFF).OfType<HabboBroadcastMessageComposer>().Should().ContainSingle();
        senior.Replies.Should().Equal("Sent to 3 players.");
    }

    [Fact]
    public async Task Alert_AtTheWholeHotel_ReachesOnlyThoseOnline()
    {
        var senior = Staff(PermissionNodes.Command.ALERT, PermissionNodes.Command.ALERT_MASS);

        await Hotel.RunAsync("alert", senior, "@online hello");

        SentTo(CAROL).Should().BeEmpty();
        SentTo(ALICE).Should().ContainSingle();
        senior.Replies.Should().Equal("Sent to 3 players.");
    }

    [Fact]
    public async Task Warn_IsTheModeratorsOwnMessage_NotAPlainPopUp()
    {
        var staff = Staff(PermissionNodes.Command.WARN);

        await Hotel.RunAsync("warn", staff, "bob Keep it friendly");

        var message = SentTo(BOB)
            .OfType<ModeratorMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;
        message.Message.Should().Be("Keep it friendly");
        message.Url.Should().BeEmpty();
        SentTo(BOB).OfType<HabboBroadcastMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task Warn_AtAGroup_NeedsTheSameMassNodeAsAlert()
    {
        var staff = Staff(PermissionNodes.Command.WARN);

        await Hotel.RunAsync("warn", staff, "@room stop");

        staff.Replies.Should().Equal("You can't use @room with that command.");
    }

    [Fact]
    public async Task RoomAlert_ReachesWhoeverWasInTheRoomWhenItWasTyped()
    {
        var staff = Staff(PermissionNodes.Command.ROOMALERT);

        await Hotel.RunAsync("roomalert", staff, "Closing in five");

        SentTo(ALICE).OfType<HabboBroadcastMessageComposer>().Should().ContainSingle();
        SentTo(BOB).OfType<HabboBroadcastMessageComposer>().Should().ContainSingle();
        SentTo(CAROL).Should().BeEmpty();
        staff.Replies.Should().Equal("Sent to 3 players.");
    }

    [Fact]
    public async Task RoomAlert_FromTheConsole_NeedsARoom()
    {
        var console = Console();

        await Hotel.RunAsync("roomalert", console, "hello");

        console.Replies.Should().Equal("That command only works in a room.");
    }

    [Fact]
    public async Task HotelAlert_ReachesEveryoneOnline_AndASlowCountdownIsNotNeeded()
    {
        var staff = Staff(PermissionNodes.Command.HOTELALERT);

        await Hotel.RunAsync("hotelalert", staff, "Event at four");

        new[] { STAFF, ALICE, BOB }
            .Select(id => SentTo(id).OfType<HabboBroadcastMessageComposer>().Count())
            .Should()
            .Equal(1, 1, 1);
        SentTo(CAROL).Should().BeEmpty();
    }

    [Fact]
    public async Task EventAlert_TellsTheHotelWhereTheEventIs_WithALinkToTheRoom()
    {
        var staff = Staff(PermissionNodes.Command.EVENTALERT);

        await Hotel.RunAsync("eventalert", staff, "Prizes for the winner");

        var message = SentTo(ALICE)
            .OfType<ModeratorMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;
        message
            .Message.Should()
            .Be("An event is on in the room Quiz night (7). Prizes for the winner");
        message.Url.Should().Be("event:navigator/goto/7");
        SentTo(CAROL).Should().BeEmpty();
    }

    [Fact]
    public async Task EventAlert_NeedsNoMessage_AndAHotelCanRewordIt()
    {
        Hotel.HotelTexts["command.eventalert.message"] = "Come to %0%!";
        var staff = Staff(PermissionNodes.Command.EVENTALERT);

        await Hotel.RunAsync("eventalert", staff, "");

        SentTo(BOB)
            .OfType<ModeratorMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Be("Come to Quiz night!");
    }

    [Fact]
    public async Task EventAlert_FromTheConsole_NeedsARoom()
    {
        var console = Console();

        await Hotel.RunAsync("eventalert", console, "");

        console.Replies.Should().Equal("That command only works in a room.");
    }

    [Fact]
    public async Task EveryAnnouncement_IsListedUnderAnnouncements()
    {
        Hotel
            .Commands.Current.Commands.Select(x => x.Category)
            .Distinct()
            .Should()
            .Equal(CommandCategories.ANNOUNCEMENTS);
    }
}
