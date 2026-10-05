using System.Collections.Immutable;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Orleans.Runtime;
using Turbo.Operations;
using Turbo.Operations.Commands;
using Turbo.Players.Permissions;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

public class AdministrationCommandsTests : OperatorCommandsTestBase
{
    private readonly HotelAvailabilityService _availability;

    /// <summary>The groups each player reaches, for the edit rule <c>:group</c> follows; default is everyone's.</summary>
    private readonly Dictionary<int, string[]> _groups = new()
    {
        [STAFF] = ["manager"],
        [BOB] = ["admin"],
    };

    public AdministrationCommandsTests()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        Hotel.Fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;
        Hotel.Fakes.Handlers["GetSnapshotAsync"] = call =>
            call.Interface == typeof(IPermissionGroupDirectoryGrain)
                ? Task.FromResult(
                    new PermissionGroupDirectorySnapshot
                    {
                        Version = 1,
                        Groups = new[]
                        {
                            Group(1, PermissionGroupNames.DEFAULT, 0),
                            Group(2, "vip", 10),
                            Group(3, "manager", 70),
                            Group(4, "admin", 100),
                        }.ToImmutableDictionary(x => x.Id),
                    }
                )
                : Fakes.NotHandled;
        Hotel.Fakes.Handlers["GetResolvedAsync"] = call =>
            call.Interface == typeof(IPlayerPermissionGrain)
                ? Task.FromResult(
                    ResolvedPermissionsSnapshot.EMPTY with
                    {
                        Granted =
                        [
                            PermissionNodes.Permissions.MANAGE,
                            .. _groups
                                .GetValueOrDefault((int)(long)call.Key!, [])
                                .Append(PermissionGroupNames.DEFAULT)
                                .Select(PermissionGroupNames.ToNode),
                        ],
                    }
                )
                : Fakes.NotHandled;

        var sessions = Hotel.Fakes.Create<Turbo.Primitives.Networking.ISessionGateway>();
        var texts = Hotel.Fakes.Create<Turbo.Primitives.Texts.IHotelTextProvider>();

        _availability = new HotelAvailabilityService(
            Config,
            Clock,
            sessions,
            Grains,
            texts,
            Hotel.Fakes.Create<IHostApplicationLifetime>(),
            new CapturingLogger<IHotelAvailability>()
        );

        Hotel.Commands.Register([
            new Turbo.Commands.ConfirmCommand(),
            new GroupCommand(
                new PermissionEditService(
                    Grains,
                    Hotel.Fakes.Create<IPermissionRegistryProvider>(),
                    Clock
                )
            ),
            new PermCommand(Grains),
            new StatusCommand(Grains, sessions, _availability, Clock),
            new OnlineCommand(Grains, sessions, Config),
            new MaintenanceCommand(_availability, Config),
            new ShutdownCommand(_availability, Config),
            new ReloadCommand(
                Hotel.Fakes.Create<Turbo.Primitives.Catalog.Providers.ICatalogSnapshotProvider<Turbo.Primitives.Catalog.Tags.NormalCatalog>>(),
                Hotel.Fakes.Create<Turbo.Primitives.Catalog.Providers.ICatalogSnapshotProvider<Turbo.Primitives.Catalog.Tags.BuildersClubCatalog>>(),
                texts,
                Hotel.Fakes.Create<Turbo.Primitives.Furniture.Providers.IFurnitureDefinitionProvider>(),
                Hotel.Fakes.Create<Turbo.Primitives.Navigator.INavigatorProvider>(),
                Hotel.Fakes.Create<Turbo.Primitives.Players.Providers.ICurrencyTypeProvider>(),
                Hotel.Fakes.Create<Turbo.Primitives.Players.Providers.IChatStyleProvider>(),
                Hotel.Fakes.Create<Turbo.Primitives.Rooms.Providers.IRoomModelProvider>(),
                Hotel.Fakes.Create<Turbo.Primitives.Pets.Providers.IPetBreedProvider>(),
                Hotel.Fakes.Create<Turbo.Primitives.Achievements.IAchievementCatalog>(),
                null! // the plugin host is a class, and no subject but plugins touches it
            ),
        ]);
    }

    [Fact]
    public async Task Group_Add_PutsThePlayerInTheGroup_AsTheyAreAskedFor()
    {
        var staff = Staff(PermissionNodes.Command.GROUP, PermissionNodes.Permissions.MANAGE);

        await Hotel.RunAsync("group", staff, "add alice VIP");

        var call = PermissionCalls("AddGroupAsync", ALICE).Should().ContainSingle().Subject;
        call.Args[0].Should().Be("vip");
        call.Args[1].Should().BeNull();
        ((PlayerId?)call.Args[3])!.Value.Value.Should().Be(STAFF);
        staff.Replies.Should().Equal("Alice is now in the group vip.");
    }

    [Fact]
    public async Task Group_Add_WithADuration_EndsByItself()
    {
        var staff = Staff(PermissionNodes.Command.GROUP, PermissionNodes.Permissions.MANAGE);

        await Hotel.RunAsync("group", staff, "add alice vip 30d");

        PermissionCalls("AddGroupAsync", ALICE).Single().Args[1].Should().Be(START.AddDays(30));
    }

    [Fact]
    public async Task Group_NeedsTheRightToManagePermissions_NotJustTheCommand()
    {
        var staff = Staff(PermissionNodes.Command.GROUP);

        await Hotel.RunAsync("group", staff, "add alice admin");

        PermissionCalls("AddGroupAsync", ALICE).Should().BeEmpty();
        staff.Replies.Should().Equal("You can't use that command.");
    }

    [Fact]
    public async Task Group_Add_OfAGroupAsHeavyAsTheirOwn_IsRefused()
    {
        // A manager with permissions.manage could once make anyone an admin, themselves included.
        var staff = Staff(PermissionNodes.Command.GROUP, PermissionNodes.Permissions.MANAGE);

        await Hotel.RunAsync("group", staff, "add alice admin");

        PermissionCalls("AddGroupAsync", ALICE).Should().BeEmpty();
        staff
            .Replies.Should()
            .Equal(
                "Only groups lighter than your heaviest are yours to hand out, and admin is not."
            );
    }

    [Fact]
    public async Task Group_OfAPlayerAsHeavyAsThem_IsRefused()
    {
        var staff = Staff(PermissionNodes.Command.GROUP, PermissionNodes.Permissions.MANAGE);

        await Hotel.RunAsync("group", staff, "remove bob vip");

        PermissionCalls("RemoveGroupAsync", BOB).Should().BeEmpty();
        staff
            .Replies.Should()
            .Equal(
                "Bob is in a group as heavy as your heaviest or heavier, so their groups are not yours to change."
            );
    }

    [Fact]
    public async Task Group_TheConsole_MayAlwaysManage()
    {
        var console = Console();

        await Hotel.RunAsync("group", console, "add alice vip");

        console.Replies.Should().Equal("Alice is now in the group vip.");
        ((PlayerId?)PermissionCalls("AddGroupAsync", ALICE).Single().Args[3]).Should().BeNull();
    }

    [Theory]
    [InlineData(PermissionChangeResultType.Unchanged, "Alice is already in the group vip.")]
    [InlineData(PermissionChangeResultType.UnknownGroup, "There is no group called vip.")]
    [InlineData(
        PermissionChangeResultType.ProtectedGroup,
        "Nobody joins or leaves the group vip by hand."
    )]
    [InlineData(PermissionChangeResultType.Expired, "Alice could not be changed (Expired).")]
    public async Task Group_Add_SaysWhyWhenItDidNotChangeAnything(
        PermissionChangeResultType result,
        string reply
    )
    {
        Hotel.Fakes.Handlers["AddGroupAsync"] = _ => Task.FromResult(result);
        var staff = Staff(PermissionNodes.Command.GROUP, PermissionNodes.Permissions.MANAGE);

        await Hotel.RunAsync("group", staff, "add alice vip");

        staff.Replies.Should().Equal(reply);
    }

    [Fact]
    public async Task Group_Remove_TakesThePermanentMembershipOut_ThenTheTemporaryOneIfThatWasNotThere()
    {
        var staff = Staff(PermissionNodes.Command.GROUP, PermissionNodes.Permissions.MANAGE);
        Hotel.Fakes.Handlers["RemoveGroupAsync"] = call =>
            Task.FromResult(
                (bool)call.Args[1]!
                    ? PermissionChangeResultType.Changed
                    : PermissionChangeResultType.NotFound
            );

        await Hotel.RunAsync("group", staff, "remove alice vip");

        PermissionCalls("RemoveGroupAsync", ALICE)
            .Select(x => x.Args[1])
            .Should()
            .Equal(false, true);
        staff.Replies.Should().Equal("Removed a vip group assignment from Alice.");
    }

    [Fact]
    public async Task Group_Remove_OfAGroupThePlayerIsNotIn_SaysSo()
    {
        var staff = Staff(PermissionNodes.Command.GROUP, PermissionNodes.Permissions.MANAGE);
        Hotel.Fakes.Handlers["RemoveGroupAsync"] = _ =>
            Task.FromResult(PermissionChangeResultType.NotFound);

        await Hotel.RunAsync("group", staff, "remove alice vip");

        staff.Replies.Should().Equal("Alice is not in the group vip.");
    }

    [Fact]
    public async Task Group_NeedsAnActionItKnows()
    {
        var staff = Staff(PermissionNodes.Command.GROUP, PermissionNodes.Permissions.MANAGE);

        await Hotel.RunAsync("group", staff, "promote alice vip");

        staff
            .Replies.Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("Usage: :group add <who> <group> [duration] | :group remove <who> <group>");
    }

    [Fact]
    public async Task Perm_Check_ExplainsWhatDecidedAndWhatItBeat()
    {
        Hotel.Fakes.Handlers["ExplainAsync"] = call =>
            Task.FromResult(
                new PermissionCheckSnapshot
                {
                    Node = (string)call.Args[0]!,
                    IsRegistered = true,
                    Granted = false,
                    Decision = new PermissionAssignmentSourceSnapshot
                    {
                        SourceType = PermissionSourceType.Player,
                        Path = [],
                        Node = "trade",
                        Value = false,
                        ExpiresAt = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc),
                    },
                    Overridden =
                    [
                        new PermissionAssignmentSourceSnapshot
                        {
                            SourceType = PermissionSourceType.Group,
                            GroupName = "vip",
                            Path = ["vip", "default"],
                            Node = "trade",
                            Value = true,
                        },
                    ],
                }
            );
        var staff = Staff(PermissionNodes.Command.PERM);

        await Hotel.RunAsync("perm", staff, "check alice trade");

        staff
            .Notices.Should()
            .ContainSingle()
            .Which.Should()
            .Equal(
                "Alice: trade is denied",
                "Decided by the player's own trade = deny until 2026-10-08 12:00 UTC",
                "Overrides group vip trade = grant via vip > default"
            );
    }

    [Fact]
    public async Task Perm_Check_OfANodeNothingSets_SaysItIsTheDefault()
    {
        Hotel.Fakes.Handlers["ExplainAsync"] = call =>
            Task.FromResult(
                new PermissionCheckSnapshot
                {
                    Node = (string)call.Args[0]!,
                    IsRegistered = false,
                    Granted = false,
                    Overridden = [],
                }
            );
        var staff = Staff(PermissionNodes.Command.PERM);

        await Hotel.RunAsync("perm", staff, "check alice made.up");

        staff
            .Notices.Single()
            .Should()
            .Equal(
                "Alice: made.up is denied (no such node is registered)",
                "Decided by nothing: no group or player sets it, and it is not granted by default"
            );
    }

    [Fact]
    public async Task Status_GivesTheGlance_AnOperatorTakesFirst()
    {
        Hotel.Fakes.Handlers["GetActiveRoomIdsAsync"] = _ =>
            Task.FromResult(ImmutableArray.Create<RoomId>(7, 8));
        Hotel.Fakes.Handlers["GetHosts"] = _ =>
            Task.FromResult(
                new Dictionary<SiloAddress, SiloStatus>
                {
                    [SiloAddress.New(new IPEndPoint(IPAddress.Loopback, 11111), 1)] =
                        SiloStatus.Active,
                    [SiloAddress.New(new IPEndPoint(IPAddress.Loopback, 11112), 1)] =
                        SiloStatus.Dead,
                }
            );
        var staff = Staff(PermissionNodes.Command.STATUS);

        await Hotel.RunAsync("status", staff, "");

        var lines = staff.Notices.Should().ContainSingle().Subject;
        lines[0].Should().StartWith("Turbo ").And.Contain(", up ");
        lines[1].Should().Be("Players online: 3, rooms loaded: 2");
        lines[2].Should().Be("Silos active: 1 of 2");
        lines[3].Should().StartWith("Memory: ");
        lines[4].Should().Be("The hotel is open");
    }

    [Fact]
    public async Task Status_SaysWhenAMaintenanceIsCountingDown()
    {
        Hotel.Fakes.Handlers["GetActiveRoomIdsAsync"] = _ =>
            Task.FromResult(ImmutableArray<RoomId>.Empty);
        Hotel.Fakes.Handlers["GetHosts"] = _ =>
            Task.FromResult(new Dictionary<SiloAddress, SiloStatus>());
        _availability.ScheduleMaintenance(TimeSpan.FromMinutes(10), "");
        var staff = Staff(PermissionNodes.Command.STATUS);

        await Hotel.RunAsync("status", staff, "");

        staff.Notices.Single()[4].Should().Be("Maintenance starts 12:10:00 UTC");
    }

    [Fact]
    public async Task Online_GivesTheCount_ToWhoeverMayAskForIt()
    {
        var staff = Staff(PermissionNodes.Command.ONLINE);

        await Hotel.RunAsync("online", staff, "");

        staff.Replies.Should().Equal("3 players are online.");
        staff.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task Online_ListsWhoByName_ForWhoeverMayAskForThat()
    {
        var staff = Staff(PermissionNodes.Command.ONLINE, PermissionNodes.Command.ONLINE_LIST);

        await Hotel.RunAsync("online", staff, "");

        staff
            .Notices.Should()
            .ContainSingle()
            .Which.Should()
            .Equal("3 players are online", "Alice, Bob, staff");
        staff.Replies.Should().BeEmpty();
    }

    [Fact]
    public async Task Online_List_StopsAtItsLimit_AndSaysHowManyItLeftOut()
    {
        for (var i = 0; i < 25; i++)
            Hotel.WithPlayer(100 + i, $"p{i:00}");
        var staff = Staff(
            PermissionNodes.Command.ONLINE,
            PermissionNodes.Command.ONLINE_LIST,
            "command.onlinesmall"
        );
        var small = new Turbo.Operations.Configuration.OperationsConfig { OnlineListMaxNames = 12 };
        Hotel.Commands.Register([
            new OnlineCommandForTest(
                Grains,
                Hotel.Fakes.Create<Turbo.Primitives.Networking.ISessionGateway>(),
                small
            ),
        ]);

        await Hotel.RunAsync("onlinesmall", staff, "");

        var lines = staff.Notices.Single();
        lines[0].Should().Be("28 players are online");
        lines.Should().HaveCount(4); // the heading, ten names, two names, and the rest
        lines[^1].Should().Be("... and 16 more");
    }

    [Fact]
    public async Task Maintenance_CountsTheHotelDown_AndOffEndsIt()
    {
        var staff = Staff(PermissionNodes.Command.MAINTENANCE);

        await Hotel.RunAsync("maintenance", staff, "10 database upgrade");

        _availability.Current.Phase.Should().Be(HotelAvailabilityPhase.Open);

        await Hotel.RunAsync("confirm", staff, "");

        _availability.Current.Phase.Should().Be(HotelAvailabilityPhase.MaintenanceScheduled);
        _availability.Current.Reason.Should().Be("database upgrade");
        _availability.Current.AtUtc.Should().Be(START.AddMinutes(10));

        await Hotel.RunAsync("maintenance", staff, "off");

        _availability.Current.Phase.Should().Be(HotelAvailabilityPhase.Open);
        staff
            .Replies.Should()
            .Equal(
                "This puts the hotel into maintenance in 10 minutes. Type :confirm within 30 seconds to go ahead.",
                "Maintenance starts in 10 minutes.",
                "The hotel is open again."
            );
    }

    [Fact]
    public async Task Maintenance_Off_WithNothingToEnd_SaysSo()
    {
        var staff = Staff(PermissionNodes.Command.MAINTENANCE);

        await Hotel.RunAsync("maintenance", staff, "off");

        staff.Replies.Should().Equal("There is no maintenance, and none is counting down.");
    }

    [Theory]
    [InlineData("soon")]
    [InlineData("-5")]
    [InlineData("1441")]
    public async Task Maintenance_NeedsMinutesInRange(string when)
    {
        var staff = Staff(PermissionNodes.Command.MAINTENANCE);

        await Hotel.RunAsync("maintenance", staff, when);

        staff.Replies.Should().Equal("Say how many minutes, from 0 to 1440, or off.");
        _availability.Current.Phase.Should().Be(HotelAvailabilityPhase.Open);
    }

    [Fact]
    public async Task Maintenance_CannotReplaceAShutdown()
    {
        _availability.ScheduleShutdown(TimeSpan.FromMinutes(5), "");
        var staff = Staff(PermissionNodes.Command.MAINTENANCE);

        await Hotel.RunAsync("maintenance", staff, "10");
        await Hotel.RunAsync("confirm", staff, "");
        await Hotel.RunAsync("maintenance", staff, "off");

        staff
            .Replies.Skip(1)
            .Should()
            .Equal(
                "A shutdown is counting down; call it off first with :shutdown cancel.",
                "There is no maintenance, and none is counting down."
            );
        _availability.Current.Phase.Should().Be(HotelAvailabilityPhase.ShutdownScheduled);
    }

    [Fact]
    public async Task Shutdown_GivenNoMinutes_CountsDownFromTheDefault_NotImmediately()
    {
        var staff = Staff(PermissionNodes.Command.SHUTDOWN);

        await Hotel.RunAsync("shutdown", staff, "");
        await Hotel.RunAsync("confirm", staff, "");

        _availability.Current.Phase.Should().Be(HotelAvailabilityPhase.ShutdownScheduled);
        _availability.Current.AtUtc.Should().Be(START.AddMinutes(5));
        staff
            .Replies.Should()
            .Equal(
                "This shuts the hotel down in 5 minutes. Type :confirm within 30 seconds to go ahead.",
                "The hotel shuts down in 5 minutes."
            );
    }

    [Fact]
    public async Task Shutdown_TakesMinutesAndAReason_AndCancelCallsItOff()
    {
        var staff = Staff(PermissionNodes.Command.SHUTDOWN);

        await Hotel.RunAsync("shutdown", staff, "15 moving servers");
        await Hotel.RunAsync("confirm", staff, "");

        _availability.Current.AtUtc.Should().Be(START.AddMinutes(15));
        _availability.Current.Reason.Should().Be("moving servers");

        await Hotel.RunAsync("shutdown", staff, "cancel");
        await Hotel.RunAsync("shutdown", staff, "cancel");

        _availability.Current.Phase.Should().Be(HotelAvailabilityPhase.Open);
        staff
            .Replies.Should()
            .Equal(
                "This shuts the hotel down in 15 minutes. Type :confirm within 30 seconds to go ahead.",
                "The hotel shuts down in 15 minutes.",
                "The shutdown is called off.",
                "No shutdown is counting down."
            );
    }

    [Fact]
    public async Task Shutdown_RefusesMinutesOutOfRange()
    {
        var staff = Staff(PermissionNodes.Command.SHUTDOWN);

        await Hotel.RunAsync("shutdown", staff, "9999");

        staff.Replies.Should().Equal("Say how many minutes, from 0 to 1440, or cancel.");
        _availability.Current.Phase.Should().Be(HotelAvailabilityPhase.Open);
    }

    [Theory]
    [InlineData("catalog", 2)] // the normal catalog and the Builders Club one
    [InlineData("texts", 1)]
    [InlineData("furni", 1)]
    [InlineData("navigator", 1)]
    [InlineData("currencies", 1)]
    [InlineData("chatstyles", 1)]
    [InlineData("roommodels", 1)]
    [InlineData("petbreeds", 1)]
    public async Task Reload_ReadsTheNamedCacheAgain_AndOnlyThat(string subject, int providers)
    {
        var staff = Staff(PermissionNodes.Command.RELOAD);

        await Hotel.RunAsync("reload", staff, subject.ToUpperInvariant());

        Hotel.Fakes.Log.Of("ReloadAsync").Should().HaveCount(providers);
        staff.Replies.Should().Equal($"Reloaded {subject}.");
    }

    [Fact]
    public async Task Reload_AnUnknownSubject_ListsNothingNew_AndReloadsNothing()
    {
        var staff = Staff(PermissionNodes.Command.RELOAD);

        await Hotel.RunAsync("reload", staff, "everything");

        Hotel.Fakes.Log.Of("ReloadAsync").Should().BeEmpty();
        staff
            .Replies.Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("everything is not a valid subject.");
    }

    private static PermissionGroupSnapshot Group(int id, string name, int weight) =>
        new()
        {
            Id = id,
            Name = name,
            DisplayName = name,
            Weight = weight,
            ParentIds = [],
            Nodes = [],
            Meta = [],
        };
}

/// <summary>The online command under another name, with a limit a test sets.</summary>
[Command("onlinesmall")]
[RequiresPermission("command.onlinesmall")]
public sealed class OnlineCommandForTest(
    Orleans.IGrainFactory grains,
    Turbo.Primitives.Networking.ISessionGateway sessions,
    Turbo.Operations.Configuration.OperationsConfig config
) : IOperatorCommand<NoArguments>
{
    private readonly OnlineCommand _inner = new(
        grains,
        sessions,
        Microsoft.Extensions.Options.Options.Create(config)
    );

    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) => _inner.ExecuteAsync(ctx, arguments, ct);
}
