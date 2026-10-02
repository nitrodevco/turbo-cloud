using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Commands;
using Turbo.Operations.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Events;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

public class SupportCommandsTests : OperatorCommandsTestBase
{
    private readonly Dictionary<int, int> _roomOf = [];
    private readonly Dictionary<(int Player, string Currency), int> _wallets = [];
    private readonly HashSet<(int Player, string Badge)> _badges = [];
    private readonly List<(int Player, int Definition)> _granted = [];
    private readonly Turbo.Primitives.Players.Providers.ICurrencyTypeProvider _currencies;

    public SupportCommandsTests()
    {
        var sessions = Hotel.Fakes.Create<Turbo.Primitives.Networking.ISessionGateway>();
        var sanctions = Hotel.Fakes.Create<Turbo.Primitives.Moderation.ISanctionService>();

        _currencies =
            Hotel.Fakes.Create<Turbo.Primitives.Players.Providers.ICurrencyTypeProvider>();
        var definitions =
            Hotel.Fakes.Create<Turbo.Primitives.Furniture.Providers.IFurnitureDefinitionProvider>();

        Hotel.Commands.Register([
            new WhoisCommand(Grains, sessions, sanctions, _currencies),
            new FollowCommand(Grains, sessions),
            new SummonCommand(Grains, sessions),
            new GiveCommand(Grains, _currencies, Config),
            new GiveBadgeCommand(Grains),
            new TakeBadgeCommand(Grains),
            new GiveItemCommand(Grains, definitions, Config),
        ]);

        var fakes = Hotel.Fakes;

        fakes.Handlers["GetEnabledCurrencyNames"] = _ =>
            (IReadOnlyCollection<string>)["credits", "duckets"];
        fakes.Handlers["TryGetCurrencyKindByName"] = call =>
        {
            var name = ((string)call.Args[0]!).ToLowerInvariant();

            // A value-type out parameter has to be given something, even for a miss.
            call.Args[1] = default(CurrencyKind);

            if (name != "credits" && name != "duckets")
                return false;

            call.Args[1] =
                name == "credits" ? CurrencyKind.Credits : CurrencyKind.ActivityPoints(0);

            return true;
        };
        fakes.Handlers["GetActiveRoomAsync"] = call =>
            Task.FromResult(
                new RoomPointerSnapshot
                {
                    RoomId = _roomOf.TryGetValue((int)(long)call.Key!, out var room) ? room : -1,
                    ActiveSinceUtc = START,
                }
            );
        fakes.Handlers["GetSummaryAsync"] = call =>
            call.Interface == typeof(IRoomGrain)
                ? Task.FromResult(
                    new RoomSummarySnapshot
                    {
                        RoomId = (int)(long)call.Key!,
                        Name = "Lobby",
                        Description = "",
                        OwnerId = STAFF,
                        OwnerName = "staff",
                        Population = 1,
                        LastUpdatedUtc = START,
                    }
                )
                : Task.FromResult(
                    new PlayerSummarySnapshot
                    {
                        PlayerId = (int)(long)call.Key!,
                        Name = "x",
                        Motto = "",
                        Figure = "",
                        Gender = AvatarGenderType.Male,
                        AchievementScore = 0,
                        BadgesRank = 0,
                        IsOnline = false,
                        CreatedAt = new DateTime(2025, 3, 4, 5, 6, 0, DateTimeKind.Utc),
                        LastUpdated = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc),
                        RespectPoints = 0,
                        RespectsLeft = 0,
                        PetRespectsLeft = 0,
                        RespectReplenishesLeft = 0,
                    }
                );
        fakes.Handlers["GetAmountForCurrencyAsync"] = call =>
            Task.FromResult(
                _wallets.TryGetValue(
                    ((int)(long)call.Key!, CurrencyName((CurrencyKind)call.Args[0]!)),
                    out var amount
                )
                    ? amount
                    : 0
            );
        fakes.Handlers["CreditRewardAsync"] = call =>
        {
            var key = ((int)(long)call.Key!, CurrencyName((CurrencyKind)call.Args[0]!));

            _wallets[key] = _wallets.GetValueOrDefault(key) + (int)call.Args[1]!;

            return Task.FromResult(true);
        };
        fakes.Handlers["TryDebitAsync"] = call =>
        {
            var request = ((List<WalletDebitRequest>)call.Args[0]!).Single();
            var key = ((int)(long)call.Key!, CurrencyName(request.CurrencyKind));

            if (_wallets.GetValueOrDefault(key) < request.Amount)
                return Task.FromResult(
                    WalletDebitResult.InsufficientBalance(
                        new WalletDebitFailure
                        {
                            CurrencyKind = request.CurrencyKind,
                            Amount = request.Amount,
                        }
                    )
                );

            _wallets[key] -= request.Amount;

            return Task.FromResult(WalletDebitResult.Success());
        };
        fakes.Handlers["GiveBadgeAsync"] = call =>
            Task.FromResult(_badges.Add(((int)(long)call.Key!, (string)call.Args[0]!)));
        fakes.Handlers["RemoveBadgeAsync"] = call =>
            Task.FromResult(_badges.Remove(((int)(long)call.Key!, (string)call.Args[0]!)));
        fakes.Handlers["TryGetDefinitionByName"] = call =>
            (string)call.Args[0]! == "throne"
                ? new FurnitureDefinitionSnapshot
                {
                    Id = 77,
                    SpriteId = 77,
                    Name = "throne",
                    ProductType = ProductType.Floor,
                    FurniCategory = FurnitureCategory.Default,
                    LogicName = "default_floor",
                    TotalStates = 1,
                    Width = 1,
                    Length = 1,
                    StackHeight = Altitude.FromInt(100),
                    CanStack = true,
                    CanWalk = false,
                    CanSit = false,
                    CanLay = false,
                    CanRecycle = true,
                    CanTrade = true,
                    CanGroup = true,
                    CanSell = true,
                    UsagePolicy = FurnitureUsageType.Everybody,
                    ExtraData = null,
                }
                : null;
        fakes.Handlers["GrantFurnitureAsync"] = call =>
        {
            _granted.Add(((int)(long)call.Key!, (int)call.Args[0]!));

            return Task.FromResult<FurnitureItemSnapshot?>(
                (FurnitureItemSnapshot)
                    RuntimeHelpers.GetUninitializedObject(typeof(FurnitureItemSnapshot))
            );
        };
    }

    private static string CurrencyName(CurrencyKind kind) =>
        kind.CurrencyType == CurrencyType.Credits ? "credits" : "duckets";

    private IEnumerable<FakeCall> Forwards(int player) =>
        Hotel
            .Fakes.Log.On<IPlayerPresenceGrain>()
            .Where(x => x.Method == "ForwardToRoomAsync" && (long)x.Key! == player);

    [Fact]
    public async Task Whois_ReportsWhereThePlayerIsTheirGroupsSanctionsAndWallet()
    {
        _roomOf[ALICE] = 7;
        _wallets[(ALICE, "credits")] = 500;
        _wallets[(ALICE, "duckets")] = 20;
        Hotel.Fakes.Handlers["GetResolvedAsync"] = call =>
            Task.FromResult(
                new ResolvedPermissionsSnapshot
                {
                    Granted =
                    [
                        PermissionNodes.Chat.SPEAK,
                        PermissionNodes.TRADE,
                        "group.default",
                        "group.vip",
                        "group.helper",
                    ],
                    Meta = ImmutableDictionary<string, string>.Empty,
                    UnregisteredNodes = [],
                    UnregisteredMetaKeys = [],
                }
            );
        var staff = Staff(PermissionNodes.Command.WHOIS);

        await Hotel.RunAsync("whois", staff, "alice");

        staff
            .Notices.Should()
            .ContainSingle()
            .Which.Should()
            .Equal(
                $"Alice (player {ALICE})",
                "Registered: 2025-03-04 05:06 UTC",
                "Online, in room 7 \"Lobby\"",
                "Groups: helper, vip",
                "Ban: none",
                "Silenced: no, trade locked: no",
                "Wallet: credits 500, duckets 20"
            );
    }

    [Fact]
    public async Task Whois_SaysWhenAPlayerIsSilencedTradeLockedAndOffline()
    {
        Hotel.Fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(
                new ResolvedPermissionsSnapshot
                {
                    Granted = ["group.default"],
                    Meta = ImmutableDictionary<string, string>.Empty,
                    UnregisteredNodes = [],
                    UnregisteredMetaKeys = [],
                }
            );
        var staff = Staff(PermissionNodes.Command.WHOIS);

        await Hotel.RunAsync("whois", staff, "carol");

        staff
            .Notices.Single()
            .Should()
            .Equal(
                $"Carol (player {CAROL})",
                "Registered: 2025-03-04 05:06 UTC",
                "Offline since 2026-09-30 08:00 UTC",
                "Groups: none",
                "Ban: none",
                "Silenced: yes, trade locked: yes",
                "Wallet: credits 0, duckets 0"
            );
    }

    [Fact]
    public async Task Follow_TakesTheExecutorToTheRoomThePlayerIsIn()
    {
        _roomOf[ALICE] = 41;
        var staff = Staff(PermissionNodes.Command.FOLLOW);

        await Hotel.RunAsync("follow", staff, "alice");

        var forward = Forwards(STAFF).Should().ContainSingle().Subject;
        ((RoomId)forward.Args[0]!).Value.Should().Be(41);
        staff.Replies.Should().Equal("Following Alice to room 41.");
    }

    [Fact]
    public async Task Follow_SaysWhyItCannot()
    {
        var staff = Staff(PermissionNodes.Command.FOLLOW);

        await Hotel.RunAsync("follow", staff, "carol"); // offline
        await Hotel.RunAsync("follow", staff, "bob"); // online, in no room
        var console = Console();
        await Hotel.RunAsync("follow", console, "bob");

        staff.Replies.Should().Equal("Carol is not online.", "Bob is not in a room.");
        console.Replies.Should().Equal("That command only works in a room.");
        Forwards(STAFF).Should().BeEmpty();
    }

    [Fact]
    public async Task Summon_SendsThePlayerToTheExecutorsRoom()
    {
        var staff = Staff(PermissionNodes.Command.SUMMON);

        await Hotel.RunAsync("summon", staff, "bob");

        ((RoomId)Forwards(BOB).Single().Args[0]!).Value.Should().Be(7);
        staff.Replies.Should().Equal("Bob is on the way.");
    }

    [Fact]
    public async Task Summon_RefusesOneselfAnOfflinePlayerAndTheConsole()
    {
        var staff = Staff(PermissionNodes.Command.SUMMON);

        await Hotel.RunAsync("summon", staff, "staff");
        await Hotel.RunAsync("summon", staff, "carol");
        var console = Console();
        await Hotel.RunAsync("summon", console, "bob");

        staff.Replies.Should().Equal("You are already here.", "Carol is not online.");
        console.Replies.Should().Equal("That command only works in a room.");
        Forwards(BOB).Should().BeEmpty();
    }

    [Fact]
    public async Task Give_AddsToABalance_OfAnyCurrencyTheHotelHas()
    {
        var staff = Staff(PermissionNodes.Command.GIVE);

        await Hotel.RunAsync("give", staff, "alice DUCKETS 50");

        _wallets[(ALICE, "duckets")].Should().Be(50);
        staff.Replies.Should().Equal("Alice was given 50 DUCKETS.");
        Hotel.Fakes.Log.Of("TrySendComposerAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task Give_OfflineRewardUsesDurableWalletReceiptWithoutSkippedNoticeWarning()
    {
        Hotel.WithPlayer(ALICE, "Alice", online: false);
        var staff = Staff(PermissionNodes.Command.GIVE);

        await Hotel.RunAsync("give", staff, "alice credits 5");

        _wallets[(ALICE, "credits")].Should().Be(5);
        staff.Replies.Should().Equal("Alice was given 5 credits.");
        Hotel.Fakes.Log.Of("CreditRewardAsync").Should().ContainSingle();
        Hotel.Fakes.Log.Of("TrySendComposerAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task Give_ANegativeAmount_TakesFromABalance_ButNeverBelowNothing()
    {
        _wallets[(ALICE, "credits")] = 100;
        var staff = Staff(PermissionNodes.Command.GIVE);

        await Hotel.RunAsync("give", staff, "alice credits -30");
        await Hotel.RunAsync("give", staff, "alice credits -500");

        _wallets[(ALICE, "credits")].Should().Be(70);
        staff
            .Replies.Should()
            .Equal(
                "Alice was given -30 credits.",
                "Alice could not be given -500 credits, a balance can't go below nothing."
            );
    }

    [Theory]
    [InlineData("0")]
    [InlineData("10000001")]
    [InlineData("-10000001")]
    public async Task Give_RefusesAnAmountOfNothingOrOverTheCap(string amount)
    {
        var staff = Staff(PermissionNodes.Command.GIVE);

        await Hotel.RunAsync("give", staff, $"alice credits {amount}");

        _wallets.Should().BeEmpty();
        staff.Replies.Should().ContainSingle().Which.Should().Contain("at most 10000000");
    }

    [Fact]
    public async Task Give_AnUnknownCurrency_ListsTheOnesThatExist()
    {
        var staff = Staff(PermissionNodes.Command.GIVE);

        await Hotel.RunAsync("give", staff, "alice gold 5");

        staff
            .Replies.Should()
            .Equal("There is no currency called gold. The currencies are: credits, duckets.");
    }

    [Fact]
    public async Task Give_AtAGroup_NeedsTheMassNode_AndIsThenLogged()
    {
        var staff = Staff(PermissionNodes.Command.GIVE);

        await Hotel.RunAsync("give", staff, "@online credits 5");

        staff.Replies.Should().Equal("You can't use @online with that command.");
        _wallets.Should().BeEmpty();

        var manager = Staff(PermissionNodes.Command.GIVE, PermissionNodes.Command.GIVE_MASS);

        await Hotel.RunAsync("give", manager, "@room credits 5");

        _wallets
            .Should()
            .BeEquivalentTo(
                new Dictionary<(int, string), int>
                {
                    [(STAFF, "credits")] = 5,
                    [(ALICE, "credits")] = 5,
                    [(BOB, "credits")] = 5,
                }
            );
        manager.Replies.Should().Equal("3 players got 5 credits.");
        await using var ctx = Hotel.Db.CreateDbContext();
        ctx.CommandLogs.Select(x => x.Arguments).Should().Equal("@room credits 5");
    }

    [Fact]
    public async Task GiveBadge_GivesIt_OrSaysThePlayerHasItAlready()
    {
        var staff = Staff(PermissionNodes.Command.GIVEBADGE);

        await Hotel.RunAsync("givebadge", staff, "alice ADM");
        await Hotel.RunAsync("givebadge", staff, "alice ADM");

        staff
            .Replies.Should()
            .Equal(
                "Alice now has the badge ADM.",
                "Alice was not given the badge ADM: they already have it, or it is not a badge."
            );
    }

    [Fact]
    public async Task GiveBadge_AtAGroup_CountsWhoGotIt()
    {
        _badges.Add((ALICE, "ADM"));
        var staff = Staff(PermissionNodes.Command.GIVEBADGE, PermissionNodes.Command.GIVE_MASS);

        var outcome = await Hotel.RunAsync("givebadge", staff, "@online ADM");

        _badges.Select(x => x.Player).Should().BeEquivalentTo([STAFF, ALICE, BOB]);
        outcome.Should().Be(CommandOutcome.Partial);
        staff
            .Replies.Should()
            .Equal(
                "Players: 2 completed, 0 partial, 1 failed, 0 unattempted. Operations: 2 succeeded, 1 refused, 0 indeterminate, 0 unattempted."
            );
    }

    [Fact]
    public async Task TakeBadge_TakesItBack_OrSaysThePlayerNeverHadIt()
    {
        _badges.Add((ALICE, "ADM"));
        var staff = Staff(PermissionNodes.Command.TAKEBADGE);

        await Hotel.RunAsync("takebadge", staff, "alice ADM");
        await Hotel.RunAsync("takebadge", staff, "alice ADM");

        staff
            .Replies.Should()
            .Equal("Alice no longer has the badge ADM.", "Alice does not have the badge ADM.");
        _badges.Should().BeEmpty();
    }

    [Fact]
    public async Task GiveItem_PutsTheFurniInTheInventory_AsManyTimesAsAsked()
    {
        var staff = Staff(PermissionNodes.Command.GIVEITEM);

        await Hotel.RunAsync("giveitem", staff, "alice throne 3");

        _granted.Should().Equal((ALICE, 77), (ALICE, 77), (ALICE, 77));
        staff.Replies.Should().Equal("Alice was given 3 x throne.");
    }

    [Fact]
    public async Task GiveItem_DefaultsToOne_AndRefusesAnUnknownFurniOrACountOutOfRange()
    {
        var staff = Staff(PermissionNodes.Command.GIVEITEM);

        await Hotel.RunAsync("giveitem", staff, "bob throne");
        await Hotel.RunAsync("giveitem", staff, "bob sofa");
        await Hotel.RunAsync("giveitem", staff, "bob throne 51");
        await Hotel.RunAsync("giveitem", staff, "bob throne 0");

        _granted.Should().Equal((BOB, 77));
        staff
            .Replies.Should()
            .Equal(
                "Bob was given 1 x throne.",
                "There is no furniture called sofa.",
                "The count must be between 1 and 50.",
                "The count must be between 1 and 50."
            );
    }

    [Fact]
    public async Task GiveItem_AtAGroup_NeedsTheMassNode()
    {
        var staff = Staff(PermissionNodes.Command.GIVEITEM);

        await Hotel.RunAsync("giveitem", staff, "@room throne");

        _granted.Should().BeEmpty();
        staff.Replies.Should().Equal("You can't use @room with that command.");
    }

    [Fact]
    public async Task GiveItem_APartialQuantity_IsReportedWithoutRetryingTheIndeterminateCall()
    {
        var grant = Hotel.Fakes.Handlers["GrantFurnitureAsync"];
        var calls = 0;
        Hotel.Fakes.Handlers["GrantFurnitureAsync"] = call =>
            ++calls == 2
                ? throw new InvalidOperationException("Unknown commit state")
                : grant(call);
        Hotel.Events.Record<CommandExecutedEvent>();
        var staff = Staff(PermissionNodes.Command.GIVEITEM, PermissionNodes.Command.LOG);

        var outcome = await Hotel.RunAsync("giveitem", staff, "alice throne 3");

        outcome.Should().Be(CommandOutcome.Partial);
        _granted.Should().ContainSingle();
        calls.Should().Be(2);
        var batch = Hotel.Events.Of<CommandExecutedEvent>().Single().Result.Batch!;
        batch.Succeeded.Should().Be(1);
        batch.Indeterminate.Should().Be(1);
        batch.Unattempted.Should().Be(1);
        SentTo(ALICE)
            .OfType<Turbo.Primitives.Messages.Outgoing.Moderation.ModeratorMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain("added 1 item(s) of throne");
        staff
            .Replies.Should()
            .ContainSingle()
            .Which.Should()
            .Contain("Operations: 1 succeeded, 0 refused, 1 indeterminate, 1 unattempted.");
        await using var db = Hotel.Db.CreateDbContext();
        db.CommandLogs.Should().ContainSingle().Which.Outcome.Should().Be("partial");
    }

    [Fact]
    public async Task Give_AFailedDebitWithinAGroup_IsPartialInTheEventAndAudit()
    {
        _wallets[(ALICE, "credits")] = 10;
        _wallets[(BOB, "credits")] = 10;
        Hotel.Events.Record<CommandExecutedEvent>();
        var staff = Staff(PermissionNodes.Command.GIVE, PermissionNodes.Command.GIVE_MASS);

        var outcome = await Hotel.RunAsync("give", staff, "@room credits -5");

        outcome.Should().Be(CommandOutcome.Partial);
        _wallets[(ALICE, "credits")].Should().Be(5);
        var batch = Hotel.Events.Of<CommandExecutedEvent>().Single().Result.Batch!;
        batch.CompletedTargets.Should().Be(2);
        batch.FailedTargets.Should().Be(1);
        await using var db = Hotel.Db.CreateDbContext();
        db.CommandLogs.Should().ContainSingle().Which.Outcome.Should().Be("partial");
    }

    [Fact]
    public async Task Give_ACallThatCommitsThenThrows_IsIndeterminateAndNeverRetried()
    {
        var credit = Hotel.Fakes.Handlers["CreditRewardAsync"];
        var calls = 0;
        Hotel.Fakes.Handlers["CreditRewardAsync"] = call =>
        {
            calls++;
            credit(call);
            throw new InvalidOperationException("Response lost after commit");
        };
        Hotel.Events.Record<CommandExecutedEvent>();
        var staff = Staff(PermissionNodes.Command.GIVE);

        var outcome = await Hotel.RunAsync("give", staff, "alice credits 5");

        outcome.Should().Be(CommandOutcome.Error);
        _wallets[(ALICE, "credits")].Should().Be(5);
        calls.Should().Be(1);
        Hotel.Events.Of<CommandExecutedEvent>().Single().Result.Batch!.Indeterminate.Should().Be(1);
    }

    [Fact]
    public async Task Confirm_PreservesTheBatchesPartialOutcome_AndDoesNotDuplicateItsReply()
    {
        Hotel.Commands.Register([new ConfirmCommand()]);
        for (var id = 100; id < 110; id++)
            Hotel.WithPlayer(id, $"p{id}");
        _badges.Add((ALICE, "ADM"));
        Hotel.Events.Record<CommandExecutedEvent>();
        var staff = Staff(PermissionNodes.Command.GIVEBADGE, PermissionNodes.Command.GIVE_MASS);
        await Hotel.RunAsync("givebadge", staff, "@online ADM");

        var outcome = await Hotel.RunAsync("confirm", staff, "");

        outcome.Should().Be(CommandOutcome.Partial);
        staff.Replies.Should().HaveCount(2);
        var confirmed = Hotel
            .Events.Of<CommandExecutedEvent>()
            .Single(x => x.Descriptor.Name == "confirm");
        confirmed.Outcome.Should().Be(CommandOutcome.Partial);
        confirmed.Result.Batch!.CompletedTargets.Should().Be(12);
    }
}
