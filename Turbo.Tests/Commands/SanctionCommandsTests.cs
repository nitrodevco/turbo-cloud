using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Operations;
using Turbo.Operations.Commands;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// What every operator command test needs: the hotel with a few players, a staff member who holds
/// the node of the command under test, and the nodes other players hold.
/// </summary>
public abstract class OperatorCommandsTestBase : IDisposable
{
    protected static readonly DateTime START = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    protected const int STAFF = 2;
    protected const int ALICE = 10;
    protected const int BOB = 11;
    protected const int CAROL = 12;

    private readonly HashSet<(int, string)> _held = [];

    protected OperatorFixture Hotel { get; } = new();

    protected SqliteDb Db { get; } = new();

    protected ManualTimeProvider Clock { get; } = new(START);

    protected IOptions<OperationsConfig> Config { get; } = Options.Create(new OperationsConfig());

    protected Orleans.IGrainFactory Grains => Hotel.Fakes.Create<Orleans.IGrainFactory>();

    protected OperatorCommandsTestBase()
    {
        Hotel
            .WithPlayer(STAFF, "staff")
            .WithPlayer(ALICE, "Alice")
            .WithPlayer(BOB, "Bob")
            .WithPlayer(CAROL, "Carol", online: false);

        Hotel.Fakes.Handlers["HasAsync"] = call =>
            call.Interface == typeof(IPlayerPermissionGrain)
                ? Task.FromResult(_held.Contains(((int)(long)call.Key!, (string)call.Args[0]!)))
                : Fakes.NotHandled;
    }

    public void Dispose()
    {
        Db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>A player holds a node, as the permission grain would answer.</summary>
    protected void Holds(int player, params string[] nodes)
    {
        foreach (var node in nodes)
            _held.Add((player, node));
    }

    /// <summary>The staff member, typing in room 7 with Alice and Bob, holding the nodes.</summary>
    protected FakeExecutor Staff(params string[] nodes)
    {
        Holds(STAFF, nodes);

        // :confirm is granted to everyone by default.
        return new FakeExecutor(
            STAFF,
            "staff",
            roomId: 7,
            roomPlayers: [STAFF, ALICE, BOB],
            [.. nodes, PermissionNodes.Command.CONFIRM]
        );
    }

    protected static FakeExecutor Console() => new(null, "console");

    /// <summary>What the presence grain of one player was asked to send, in order.</summary>
    protected IEnumerable<object?> SentTo(int player) =>
        Hotel
            .Fakes.Log.Calls.Where(x => x.Method is "SendComposerAsync" or "TrySendComposerAsync")
            .Where(x => x.Key is long key && key == player)
            .Where(x =>
                x.Method != "TrySendComposerAsync" || Hotel.Online.Any(id => id.Value == player)
            )
            .Select(x => x.Args[0]);

    protected IEnumerable<FakeCall> PermissionCalls(string method, int player) =>
        Hotel
            .Fakes.Log.On<IPlayerPermissionGrain>()
            .Where(x => x.Method == method && x.Key is long key && key == player);
}

public class SanctionCommandsTests : OperatorCommandsTestBase
{
    private readonly SanctionService _sanctions;
    private readonly List<(int Player, object? Farewell)> _disconnects = [];

    public SanctionCommandsTests()
    {
        _sanctions = new SanctionService(
            Db,
            Clock,
            new TestEventBus().System,
            NullLogger<ISanctionService>.Instance
        );

        Hotel.Fakes.Handlers["DisconnectPlayerAsync"] = call =>
        {
            var player = ((Turbo.Primitives.Players.PlayerId)call.Args[0]!).Value;
            var online = Hotel.Online.Any(x => x.Value == player);

            if (online)
                _disconnects.Add((player, call.Args[1]));

            return Task.FromResult(online);
        };

        var sessions = Hotel.Fakes.Create<Turbo.Primitives.Networking.ISessionGateway>();
        var texts = Hotel.Fakes.Create<Turbo.Primitives.Texts.IHotelTextProvider>();

        Hotel.Commands.Register([
            new BanCommand(
                _sanctions,
                sessions,
                Grains,
                texts,
                Clock,
                new CapturingLogger<BanCommand>()
            ),
            new UnbanCommand(_sanctions),
            new SilenceCommand(Grains, Clock),
            new UnsilenceCommand(Grains),
            new TradelockCommand(Grains, Clock),
            new UntradelockCommand(Grains),
            new DisconnectCommand(Grains, sessions, texts),
        ]);
    }

    [Fact]
    public async Task Ban_KeepsThePlayerOut_TellsThemWhy_AndSendsThemHome()
    {
        var staff = Staff(PermissionNodes.Command.BAN);

        var outcome = await Hotel.RunAsync("ban", staff, "alice 7d  spamming the lobby ");

        outcome.Should().Be(CommandOutcome.Completed);
        staff.Replies.Should().Equal("Alice is banned: 7 d.");
        var ban = await _sanctions.GetActiveBanAsync(ALICE, CancellationToken.None);
        ban!.Reason.Should().Be("spamming the lobby");
        ban.ExpiresAtUtc.Should().Be(START.AddDays(7));
        ban.IssuerId!.Value.Value.Should().Be(STAFF);
        var (player, farewell) = _disconnects.Should().ContainSingle().Subject;
        player.Should().Be(ALICE);
        farewell
            .Should()
            .BeOfType<UserBannedMessageComposer>()
            .Which.Message.Should()
            .Be(
                "You are banned from the hotel until 2026-10-08 12:00 UTC. Reason: spamming the lobby"
            );
    }

    [Fact]
    public async Task Ban_ForGood_SaysSo_AndAnOfflinePlayerIsBannedToo()
    {
        var staff = Staff(PermissionNodes.Command.BAN);

        await Hotel.RunAsync("ban", staff, "carol perm");

        (await _sanctions.GetActiveBanAsync(CAROL, CancellationToken.None))!
            .ExpiresAtUtc.Should()
            .BeNull();
        staff.Replies.Should().Equal("Carol is banned: permanent.");
        _disconnects.Should().BeEmpty();
    }

    [Fact]
    public async Task Ban_GivenNoReason_SaysNoneWasGiven()
    {
        await Hotel.RunAsync("ban", Staff(PermissionNodes.Command.BAN), "bob 1h");

        (await _sanctions.GetActiveBanAsync(BOB, CancellationToken.None))!
            .Reason.Should()
            .Be("No reason given");
    }

    [Fact]
    public async Task Ban_CannotBeAimedAtOneselfOrAtOtherStaff_ButTheConsoleCan()
    {
        Holds(ALICE, PermissionNodes.Command.BAN);
        var staff = Staff(PermissionNodes.Command.BAN);

        await Hotel.RunAsync("ban", staff, "staff perm");
        await Hotel.RunAsync("ban", staff, "alice perm");
        var console = Console();
        await Hotel.RunAsync("ban", console, "alice perm");

        staff.Replies.Should().Equal("You can't ban staff.", "You can't ban Alice.");
        console.Replies.Should().Equal("Alice is banned: permanent.");
    }

    [Fact]
    public async Task Ban_NeedsADuration_NotABareNumber()
    {
        var staff = Staff(PermissionNodes.Command.BAN);

        await Hotel.RunAsync("ban", staff, "alice 7");

        staff
            .Replies.Should()
            .Equal("7 is not a valid duration. Usage: :ban <who> <duration> [reason]");
        (await _sanctions.GetActiveBanAsync(ALICE, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Unban_LiftsTheBan_OrSaysThereWasNone()
    {
        var staff = Staff(PermissionNodes.Command.UNBAN);
        await _sanctions.BanAsync(ALICE, null, "spam", null, CancellationToken.None);

        await Hotel.RunAsync("unban", staff, "alice");
        await Hotel.RunAsync("unban", staff, "alice");

        staff.Replies.Should().Equal("Alice is no longer banned.", "Alice is not banned.");
        (await _sanctions.GetActiveBanAsync(ALICE, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Silence_DeniesSpeaking_UntilTheTimeIsUp_AndTellsThePlayer()
    {
        var staff = Staff(PermissionNodes.Command.SILENCE);

        await Hotel.RunAsync("silence", staff, "alice 2h");

        var call = PermissionCalls("SetNodeAsync", ALICE).Should().ContainSingle().Subject;
        call.Args[0].Should().Be(PermissionNodes.Chat.SPEAK);
        call.Args[1].Should().Be(false);
        call.Args[2].Should().Be(START.AddHours(2));
        ((Turbo.Primitives.Players.PlayerId?)call.Args[4])!.Value.Value.Should().Be(STAFF);
        staff.Replies.Should().Equal("Alice is silenced: 2 h.");
        SentTo(ALICE)
            .OfType<ModeratorMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Be("You have been silenced: 2 h.");
    }

    [Fact]
    public async Task Silence_ForGood_HasNoExpiry_AndAnOfflinePlayerIsNotWokenToBeTold()
    {
        var staff = Staff(PermissionNodes.Command.SILENCE);

        await Hotel.RunAsync("silence", staff, "carol perm");

        PermissionCalls("SetNodeAsync", CAROL).Single().Args[2].Should().BeNull();
        SentTo(CAROL).Should().BeEmpty();
    }

    [Fact]
    public async Task Silence_SaysSoWhenThePlayerAlreadyIs_AndWhenItCouldNotBeDone()
    {
        var staff = Staff(PermissionNodes.Command.SILENCE);
        var results = new Queue<PermissionChangeResultType>([
            PermissionChangeResultType.Unchanged,
            PermissionChangeResultType.Expired,
        ]);
        Hotel.Fakes.Handlers["SetNodeAsync"] = _ => Task.FromResult(results.Dequeue());

        await Hotel.RunAsync("silence", staff, "alice 1h");
        await Hotel.RunAsync("silence", staff, "alice 1h");

        staff
            .Replies.Should()
            .Equal("Alice is already silenced.", "Alice could not be silenced (Expired).");
        SentTo(ALICE).Should().BeEmpty();
    }

    [Fact]
    public async Task Silence_IsStoppedAtStaff()
    {
        Holds(ALICE, PermissionNodes.Command.SILENCE);
        var staff = Staff(PermissionNodes.Command.SILENCE);

        await Hotel.RunAsync("silence", staff, "alice 1h");

        staff.Replies.Should().Equal("You can't silence Alice.");
        PermissionCalls("SetNodeAsync", ALICE).Should().BeEmpty();
    }

    [Fact]
    public async Task Unsilence_RemovesTheDenial_WhetherItWasForATimeOrForGood()
    {
        var staff = Staff(PermissionNodes.Command.SILENCE);
        Hotel.Fakes.Handlers["UnsetNodeAsync"] = call =>
            Task.FromResult(
                (bool)call.Args[1]!
                    ? PermissionChangeResultType.Changed
                    : PermissionChangeResultType.NotFound
            );

        await Hotel.RunAsync("unsilence", staff, "alice");

        staff.Replies.Should().Equal("Removed the hotel silence from Alice.");
        PermissionCalls("UnsetNodeAsync", ALICE)
            .Select(x => (x.Args[0], x.Args[1]))
            .Should()
            .Equal((PermissionNodes.Chat.SPEAK, true), (PermissionNodes.Chat.SPEAK, false));
    }

    [Fact]
    public async Task Unsilence_OfSomeoneNotSilenced_SaysSo()
    {
        var staff = Staff(PermissionNodes.Command.SILENCE);
        Hotel.Fakes.Handlers["UnsetNodeAsync"] = _ =>
            Task.FromResult(PermissionChangeResultType.NotFound);

        await Hotel.RunAsync("unsilence", staff, "alice");

        staff.Replies.Should().Equal("Alice is not silenced.");
    }

    [Fact]
    public async Task Tradelock_DeniesTheTradeNode_ForATime()
    {
        var staff = Staff(PermissionNodes.Command.TRADELOCK);

        await Hotel.RunAsync("tradelock", staff, "bob 7d");

        var call = PermissionCalls("SetNodeAsync", BOB).Should().ContainSingle().Subject;
        call.Args[0].Should().Be(PermissionNodes.TRADE);
        call.Args[1].Should().Be(false);
        call.Args[2].Should().Be(START.AddDays(7));
        staff.Replies.Should().Equal("Bob can't trade: 7 d.");
    }

    [Fact]
    public async Task Untradelock_LiftsIt_UnderTheSameNodeAsTheLock()
    {
        var staff = Staff(PermissionNodes.Command.TRADELOCK);

        await Hotel.RunAsync("untradelock", staff, "bob");

        staff.Replies.Should().Equal("Removed a trading lock from Bob.");
        PermissionCalls("UnsetNodeAsync", BOB)
            .Select(x => x.Args[0])
            .Should()
            .OnlyContain(x => (string)x! == PermissionNodes.TRADE);
    }

    [Fact]
    public async Task Disconnect_ClosesTheConnection_OfAPlayerWhoIsHere()
    {
        var staff = Staff(PermissionNodes.Command.DISCONNECT);

        await Hotel.RunAsync("disconnect", staff, "alice");
        await Hotel.RunAsync("disconnect", staff, "carol");

        staff.Replies.Should().Equal("Alice is disconnected.", "Carol is not online.");
        _disconnects.Should().ContainSingle().Which.Player.Should().Be(ALICE);
        _disconnects[0]
            .Farewell.Should()
            .BeOfType<ModeratorMessageComposer>()
            .Which.Message.Should()
            .Contain("reconnect");
    }

    [Fact]
    public async Task Disconnect_IsStoppedAtStaff()
    {
        Holds(ALICE, PermissionNodes.Command.DISCONNECT);
        var staff = Staff(PermissionNodes.Command.DISCONNECT);

        await Hotel.RunAsync("disconnect", staff, "alice");

        staff.Replies.Should().Equal("You can't disconnect Alice.");
        _disconnects.Should().BeEmpty();
    }

    [Fact]
    public async Task ABan_IsASanctionRow_OfKindBan_ForTheServiceToEnforce()
    {
        await Hotel.RunAsync("ban", Staff(PermissionNodes.Command.BAN), "alice 1d");

        (await _sanctions.GetActiveBanAsync(ALICE, CancellationToken.None))!
            .Kind.Should()
            .Be(SanctionKind.Ban);
    }

    [Fact]
    public async Task ASavedBanIsReportedAsSavedWhenDisconnectFails()
    {
        Hotel.Fakes.Handlers["DisconnectPlayerAsync"] = _ =>
            throw new InvalidOperationException("Connection close failed");
        var staff = Staff(PermissionNodes.Command.BAN);
        var outcome = await Hotel.RunAsync("ban", staff, "alice 1d");
        outcome.Should().Be(CommandOutcome.Completed);
        (await _sanctions.GetActiveBanAsync(ALICE, TestContext.Current.CancellationToken))
            .Should()
            .NotBeNull();
        staff.Replies.Should().ContainSingle().Which.Should().Contain("ban for Alice was saved");
    }
}
