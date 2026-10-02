using FluentAssertions;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Events;
using Turbo.Primitives.Rooms.Enums;
using Xunit;

namespace Turbo.Tests.Commands;

public class ChatCommandTests
{
    private readonly CommandRoomFixture _room = new();
    private readonly ProbeCommand _probe = new();

    private void Install(params ICommand[] commands) => _room.Commands.Register(commands);

    [Fact]
    public async Task ACommandRunsInsteadOfBeingSaid()
    {
        Install(_probe);
        _room.AddPlayer(2, "command.probe");

        var handled = await _room.SayAsync(2, ":probe hello there");

        handled.Should().BeTrue();
        _probe.Texts.Should().Equal("hello there");
        _room.SaidToRoom().Should().BeEmpty();
    }

    [Fact]
    public async Task AnAliasRunsTheCommandToo()
    {
        Install(_probe);
        _room.AddPlayer(2, "command.probe");

        await _room.SayAsync(2, ":SONDA x");

        _probe.Texts.Should().Equal("x");
    }

    [Theory]
    [InlineData(":unknownword")]
    [InlineData(":)")]
    [InlineData("probe")]
    [InlineData(": probe")]
    public async Task ALineThatIsNotACommandIsSaidAsChat(string line)
    {
        Install(_probe);
        _room.AddPlayer(2, "command.probe");

        await _room.SayAsync(2, line);

        _probe.Texts.Should().BeEmpty();
        _room.SaidToRoom().Should().Equal(line);
    }

    [Fact]
    public async Task AWhisperIsNeverACommand()
    {
        Install(_probe);
        _room.AddPlayer(2, "command.probe");
        _room.AddPlayer(3);

        await _room.SayAsync(2, ":probe secret", RoomChatType.Whisper, "player3");

        _probe.Texts.Should().BeEmpty();
    }

    [Fact]
    public async Task ShoutingACommandRunsIt()
    {
        Install(_probe);
        _room.AddPlayer(2, "command.probe");

        await _room.SayAsync(2, ":probe loud", RoomChatType.Shout);

        _probe.Texts.Should().Equal("loud");
    }

    [Fact]
    public async Task AKnownCommandWithoutItsNodeIsSwallowedWithARefusal()
    {
        Install(_probe);
        _room.AddPlayer(2);

        var handled = await _room.SayAsync(2, ":probe nope");

        handled.Should().BeTrue();
        _probe.Texts.Should().BeEmpty();
        _room.SaidToRoom().Should().BeEmpty();
        _room
            .WhispersTo(2)
            .Should()
            .Equal(CommandReplyKeys.Defaults[CommandReplyKeys.NO_PERMISSION]);
    }

    [Fact]
    public async Task AMutedPlayerCannotRunCommands()
    {
        Install(_probe);
        _room.AddPlayer(2, "command.probe");
        _room.MuteForAWhile(2);

        await _room.SayAsync(2, ":probe quiet");

        _probe.Texts.Should().BeEmpty();
    }

    [Fact]
    public async Task ArgumentsReachTheCommandUnfiltered_AndChatIsStillFiltered()
    {
        Install(_probe);
        _room.AddPlayer(2, "command.probe");
        _room.FilterWord("heck");

        await _room.SayAsync(2, ":probe what the heck");
        await _room.SayAsync(2, "what the heck");

        _probe.Texts.Should().Equal("what the heck");
        _room.SaidToRoom().Should().ContainSingle().Which.Should().NotContain("heck");
    }

    [Fact]
    public async Task AnOverlengthKnownCommandIsRejectedBeforeTruncation()
    {
        Install(_probe);
        _room.AddPlayer(2, "command.probe");
        _room.HotelTexts[CommandReplyKeys.TOO_LONG] = "That command exceeds the chat limit.";
        var input = ":probe " + new string('x', 200);

        await _room.SayAsync(2, input);

        _probe.Texts.Should().BeEmpty();
        _room.SaidToRoom().Should().BeEmpty();
        _room.WhispersTo(2).Should().Equal("That command exceeds the chat limit.");
    }

    [Fact]
    public async Task OrdinaryChatStillTruncatesToTheConfiguredLimit()
    {
        _room.AddPlayer(2);
        var input = new string('x', 200);

        await _room.SayAsync(2, input);

        _room.SaidToRoom().Should().ContainSingle().Which.Should().Be(input[..100]);
    }

    [Fact]
    public async Task ArgumentsThatDoNotFit_AreAnsweredWithGeneratedUsage()
    {
        var count = new CountCommand();
        Install(count);
        _room.AddPlayer(2, "command.count");

        await _room.SayAsync(2, ":count");
        await _room.SayAsync(2, ":count lots");

        count.Calls.Should().Be(0);
        _room
            .WhispersTo(2)
            .Should()
            .Equal("Usage: :count <count>", "lots is not a valid count. Usage: :count <count>");
        _room.SaidToRoom().Should().BeEmpty();
    }

    [Fact]
    public async Task ACommandNeedingRoomRights_IsRefusedWithoutThem_AndRunsWith()
    {
        var guarded = new GuardedCommand();
        Install(guarded);
        _room.AddPlayer(2, "command.guarded");

        await _room.SayAsync(2, ":guarded");
        guarded.Calls.Should().Be(0);
        _room
            .WhispersTo(2)
            .Should()
            .Equal(CommandReplyKeys.Defaults[CommandReplyKeys.NEEDS_ROOM_LEVEL]);

        _room.GiveRights(2);
        await _room.SayAsync(2, ":guarded");
        guarded.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ACommandThatThrows_IsContained_AndTheLineIsNotSaid()
    {
        _probe.Throws = true;
        Install(_probe);
        _room.AddPlayer(2, "command.probe");
        _room.Events.Record<CommandExecutedEvent>();

        var handled = await _room.SayAsync(2, ":probe boom");

        handled.Should().BeTrue();
        _room.SaidToRoom().Should().BeEmpty();
        _room.WhispersTo(2).Should().Equal(CommandReplyKeys.Defaults[CommandReplyKeys.FAILED]);
        _room.Events.Of<CommandExecutedEvent>().Single().Outcome.Should().Be(CommandOutcome.Error);
    }

    [Fact]
    public async Task APluginCanVetoACommandFromItsOwnState()
    {
        Install(_probe);
        _room.AddPlayer(2, "command.probe");
        _room.Events.On<CommandExecutingEvent>(e =>
        {
            e.Descriptor.Type.Should().Be<ProbeCommand>();
            e.Cancel();
        });
        _room.Events.Record<CommandExecutedEvent>();

        await _room.SayAsync(2, ":probe vetoed");

        _probe.Texts.Should().BeEmpty();
        _room.Events.Of<CommandExecutedEvent>().Single().Outcome.Should().Be(CommandOutcome.Vetoed);
    }

    [Fact]
    public async Task ACompletedCommandRaisesTheExecutedEvent()
    {
        Install(_probe);
        _room.AddPlayer(2, "command.probe");
        _room.Events.Record<CommandExecutedEvent>();

        await _room.SayAsync(2, ":probe go");

        var executed = _room.Events.Of<CommandExecutedEvent>().Single();
        executed.Outcome.Should().Be(CommandOutcome.Completed);
        executed.Descriptor.Name.Should().Be("probe");
    }

    [Fact]
    public async Task AStatusIsWhisperedFromTheCommandsOwnText_WithItsParameters()
    {
        _probe.Result = CommandResult.Done("greeted", "Alice");
        Install(_probe);
        _room.AddPlayer(2, "command.probe");

        await _room.SayAsync(2, ":probe");

        _room.WhispersTo(2).Should().Equal("Hello Alice!");
    }

    [Fact]
    public async Task ACommandReturningFailure_PublishesFailure_AndKeepsItsReply()
    {
        _probe.Result = CommandResult.Fail("greeted", "Alice");
        Install(_probe);
        _room.AddPlayer(2, "command.probe");
        _room.Events.Record<CommandExecutedEvent>();

        await _room.SayAsync(2, ":probe");

        _room.Events.Of<CommandExecutedEvent>().Single().Outcome.Should().Be(CommandOutcome.Failed);
        _room.WhispersTo(2).Should().Equal("Hello Alice!");
    }

    [Fact]
    public async Task AHotelTextOverridesTheDefault()
    {
        _probe.Result = CommandResult.Done("greeted", "Alice");
        Install(_probe);
        _room.AddPlayer(2, "command.probe");
        _room.HotelTexts["command.probe.greeted"] = "Hola %0%";

        await _room.SayAsync(2, ":probe");

        _room.WhispersTo(2).Should().Equal("Hola Alice");
    }

    [Fact]
    public async Task CommandsOverTheFloodLimitAreDropped_WithoutMutingChatOrLockingTheClient()
    {
        Install(_probe);
        _room.AddPlayer(2, "command.probe");
        var limit = _room.FloodLimit;

        for (var i = 0; i < limit + 3; i++)
            await _room.SayAsync(2, ":probe");

        _probe.Texts.Should().HaveCount(limit);
        _room
            .WhispersTo(2)
            .Should()
            .HaveCount(3)
            .And.AllBe(CommandReplyKeys.Defaults[CommandReplyKeys.FLOOD]);
        _room.FloodNoticesTo(2).Should().BeEmpty();
    }

    [Fact]
    public async Task ChatOverTheFloodLimit_StillMutesAndTellsTheClient()
    {
        _room.AddPlayer(2);
        var limit = _room.FloodLimit;

        for (var i = 0; i < limit + 1; i++)
            await _room.SayAsync(2, "hello");

        _room.SaidToRoom().Should().HaveCount(limit);
        _room.FloodNoticesTo(2).Should().ContainSingle();
    }
}
