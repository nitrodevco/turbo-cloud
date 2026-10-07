using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Players.Grains.Subscriptions;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Grains.Subscriptions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// A membership held by permission: a player holding <c>club.habbo_club.unlimited</c> or
/// <c>club.builders_club.unlimited</c> is a member of that club, at the highest Builders Club
/// furni limit, for as long as they hold it, without a row being written. A node that runs out
/// shows its own end; a permanent one shows 7 days of Habbo Club or 24 hours of Builders Club
/// left, which never come closer. Granting or taking the node away tells everything that reads
/// the membership.
/// </summary>
public sealed class UnlimitedClubPermissionTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    /// <summary>The nodes held, and until when; null for ever.</summary>
    private readonly Dictionary<string, DateTime?> _held = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public UnlimitedClubPermissionTests()
    {
        _fakes.Handlers["ExplainInterleavedAsync"] = call =>
        {
            var node = (string)call.Args[0]!;
            var held = _held.TryGetValue(node, out var until);

            return Task.FromResult(
                new PermissionCheckSnapshot
                {
                    Node = node,
                    IsRegistered = true,
                    Granted = held,
                    Decision = held
                        ? new PermissionAssignmentSourceSnapshot
                        {
                            SourceType = PermissionSourceType.Player,
                            Path = [],
                            Node = node,
                            Value = true,
                            ExpiresAt = until,
                            GrantedUntil = until,
                        }
                        : null,
                    Overridden = [],
                }
            );
        };
        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "builder",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
    }

    public void Dispose() => _db.Dispose();

    private IPlayerSubscriptionGrain Grain() =>
        (IPlayerSubscriptionGrain)
            GrainHarness.Create(
                typeof(PlayerModule).Assembly,
                "Turbo.Players.Grains.Subscriptions.PlayerSubscriptionGrain",
                _fakes,
                _db
            );

    [Fact]
    public async Task WithoutTheNode_APlayerWhoNeverBoughtIsNoMember()
    {
        var grain = Grain();

        (await grain.HasActiveAsync(SubscriptionType.HabboClub, Ct)).Should().BeFalse();

        var builders = await grain.GetAsync(SubscriptionType.BuildersClub, Ct);

        builders.IsActive.Should().BeFalse();
        builders.FurniLimit.Should().Be(10, "the trial limit");
    }

    [Fact]
    public async Task APermanentHabboClubNode_MakesThemAMember_ShowingAWeekLeft_WithoutWritingAMembership()
    {
        _held[PermissionNodes.Club.HABBO_CLUB_UNLIMITED] = null;
        var grain = Grain();

        var club = await grain.GetAsync(SubscriptionType.HabboClub, Ct);

        club.IsActive.Should().BeTrue();
        club.DaysRemaining.Should().Be(7);
        (await grain.HasActiveAsync(SubscriptionType.BuildersClub, Ct))
            .Should()
            .BeFalse("each club has its own node");

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.PlayerSubscriptions.AnyAsync(Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task APermanentBuildersClubNode_LendsAtTheHighestLimit_ShowingADayLeft()
    {
        _held[PermissionNodes.Club.BUILDERS_CLUB_UNLIMITED] = null;
        var grain = Grain();

        // A membership bought long ago and lapsed is still a running one while the node is held.
        await grain.ExtendAsync(SubscriptionType.BuildersClub, 1, Ct);
        await using (var db = await _db.CreateDbContextAsync(Ct))
            await db.PlayerSubscriptions.ExecuteUpdateAsync(
                s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddDays(-30)),
                Ct
            );

        var builders = await Grain().GetAsync(SubscriptionType.BuildersClub, Ct);

        builders.IsActive.Should().BeTrue();
        builders.SecondsLeft.Should().BeInRange(24 * 3600 - 60, 24 * 3600);
        builders.FurniLimit.Should().Be(1000);
        builders.MaxFurniLimit.Should().Be(1000);

        _held.Clear();

        (await Grain().HasActiveAsync(SubscriptionType.BuildersClub, Ct))
            .Should()
            .BeFalse("without the node it is the membership they bought");
    }

    [Fact]
    public async Task ANodeThatRunsOut_ShowsItsOwnEnd_UnlessTheyBoughtLonger()
    {
        var nodeEnds = DateTime.UtcNow.AddDays(40);
        _held[PermissionNodes.Club.HABBO_CLUB_UNLIMITED] = nodeEnds;
        _held[PermissionNodes.Club.BUILDERS_CLUB_UNLIMITED] = nodeEnds;
        var grain = Grain();

        (await grain.GetAsync(SubscriptionType.HabboClub, Ct))
            .ExpiresAt.Should()
            .BeCloseTo(nodeEnds, TimeSpan.FromSeconds(1));

        await grain.ExtendAsync(SubscriptionType.BuildersClub, 90, Ct);

        (await grain.GetAsync(SubscriptionType.BuildersClub, Ct))
            .DaysRemaining.Should()
            .Be(90, "what they bought outlasts the node");
    }

    [Fact]
    public async Task GrantingOrTakingTheNode_TellsTheMembershipItChanged()
    {
        var handler = new UnlimitedClubPermissionsHandler(_fakes.Create<Orleans.IGrainFactory>());

        await handler.HandleAsync(
            new PlayerPermissionsChangedEvent
            {
                PlayerId = 1,
                Previous = Holding(),
                Current = Holding(PermissionNodes.Club.BUILDERS_CLUB_UNLIMITED, "room.enter.full"),
            },
            null!,
            Ct
        );
        await handler.HandleAsync(
            new PlayerPermissionsChangedEvent
            {
                PlayerId = 1,
                Previous = Holding("room.enter.full"),
                Current = Holding(),
            },
            null!,
            Ct
        );

        _fakes
            .Log.Calls.Where(x => x.Method == "OnChangedAsync")
            .Select(x => (SubscriptionType)x.Args[0]!)
            .Should()
            .Equal(SubscriptionType.BuildersClub);
    }

    private static ResolvedPermissionsSnapshot Holding(params string[] nodes) =>
        new()
        {
            Granted = [.. nodes],
            Meta = ImmutableDictionary<string, string>.Empty,
            UnregisteredNodes = [],
            UnregisteredMetaKeys = [],
        };
}
