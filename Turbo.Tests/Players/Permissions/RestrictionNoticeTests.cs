using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Turbo.Database.Entities.Permissions;
using Turbo.Players;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

public class RestrictionNoticeTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class Notices : IPlayerNoticeService
    {
        public bool Online { get; set; } = true;
        public List<string> Delivered { get; } = [];
        public int Attempts { get; private set; }

        public Task<PlayerNoticeDelivery> SendCurrencyRewardAsync(
            PlayerId playerId,
            long amount,
            CurrencyTypeSnapshot currency,
            CancellationToken ct
        ) => throw new NotSupportedException();

        public Task<PlayerNoticeDelivery> SendAsync(
            PlayerId playerId,
            string textKey,
            string defaultText,
            IReadOnlyList<string> parameters,
            CancellationToken ct
        )
        {
            Attempts++;
            if (!Online)
                return Task.FromResult(PlayerNoticeDelivery.Offline);
            Delivered.Add(textKey);
            return Task.FromResult(PlayerNoticeDelivery.Sent);
        }
    }

    private sealed class Hotel
    {
        private const BindingFlags ALL =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly object _grain;
        private readonly object _state;
        public Clock Clock { get; } = new();
        public Notices Notices { get; } = new();
        public InMemoryDb Db { get; } = new();
        public IPlayerPermissionGrain Grain => (IPlayerPermissionGrain)_grain;
        public Dictionary<
            (string Node, bool Temporary),
            PermissionNodeAssignmentSnapshot
        > Nodes { get; }

        public Hotel()
        {
            var fakes = new Fakes();
            var registry = new PermissionRegistry([new CorePermissionNodeSource()]);
            fakes.Handlers["get_Current"] = _ => registry;
            _grain = GrainHarness.Create(
                typeof(PlayerModule).Assembly,
                "Turbo.Players.Grains.Permissions.PlayerPermissionGrain",
                fakes,
                Db
            );
            SetField("_permissionRegistryProvider", fakes.Create<IPermissionRegistryProvider>());
            SetField("_timeProvider", Clock);
            SetField("_noticeService", Notices);
            _state = _grain.GetType().GetField("_state", ALL)!.GetValue(_grain)!;
            Nodes =
                (Dictionary<(string, bool), PermissionNodeAssignmentSnapshot>)
                    _state.GetType().GetProperty("NodesByNode", ALL)!.GetValue(_state)!;
            Nodes[(PermissionNodes.Chat.SPEAK, false)] = Node(PermissionNodes.Chat.SPEAK, true);
            Nodes[(PermissionNodes.TRADE, false)] = Node(PermissionNodes.TRADE, true);
        }

        private void SetField(string name, object value) =>
            _grain.GetType().GetField(name, ALL)!.SetValue(_grain, value);

        public void Groups(params PermissionGroupSnapshot[] groups) =>
            _state
                .GetType()
                .GetProperty("Groups", ALL)!
                .SetValue(
                    _state,
                    new PermissionGroupDirectorySnapshot
                    {
                        Version = 1,
                        Groups = groups.ToImmutableDictionary(x => x.Id),
                    }
                );

        public void Resolve() =>
            _grain.GetType().GetMethod("Resolve", ALL)!.Invoke(_grain, [true, null]);

        public void Restrict(string node, bool temporary = true) =>
            Nodes[(node, temporary)] = Node(
                node,
                false,
                temporary ? Clock.Now.AddMinutes(1) : null
            );
    }

    private static PermissionNodeAssignmentSnapshot Node(
        string node,
        bool granted,
        DateTime? expiry = null
    ) =>
        new()
        {
            Node = node,
            Value = granted,
            ExpiresAt = expiry,
        };

    [Fact]
    public async Task LoginAndReconnect_ReportCurrentRestrictions_ButClientResendsDoNotDuplicateThem()
    {
        var hotel = new Hotel();
        hotel.Restrict(PermissionNodes.Chat.SPEAK);
        hotel.Restrict(PermissionNodes.TRADE);
        hotel.Resolve();
        await hotel.Grain.NotifyActiveRestrictionsAsync(CancellationToken.None);
        await hotel.Grain.SendPermissionNodesAsync(CancellationToken.None);
        await hotel.Grain.SendClientStateAsync(CancellationToken.None);
        hotel
            .Notices.Delivered.Should()
            .Equal("player.restriction.chat.active", "player.restriction.trade.active");
        await hotel.Grain.NotifyActiveRestrictionsAsync(CancellationToken.None);
        hotel.Notices.Delivered.Should().HaveCount(4);
    }

    [Fact]
    public async Task Expiry_RestoresActualPermission_AndNotifiesOnlyOnce()
    {
        var hotel = new Hotel();
        hotel.Restrict(PermissionNodes.Chat.SPEAK);
        hotel.Resolve();
        hotel.Clock.Now = hotel.Clock.Now.AddMinutes(2);
        hotel.Resolve();
        hotel.Resolve();
        await hotel.Grain.SendPermissionNodesAsync(CancellationToken.None);
        hotel.Notices.Delivered.Should().Equal("player.restriction.chat.restored");
    }

    [Fact]
    public void ExpiredTemporaryDenial_DoesNotRestoreWhenAPermanentDenialRemains()
    {
        var hotel = new Hotel();
        hotel.Restrict(PermissionNodes.Chat.SPEAK, temporary: false);
        hotel.Restrict(PermissionNodes.Chat.SPEAK);
        hotel.Resolve();
        hotel.Clock.Now = hotel.Clock.Now.AddMinutes(2);
        hotel.Resolve();
        hotel.Notices.Delivered.Should().BeEmpty();
    }

    [Fact]
    public void ManualRemoval_DoesNotProduceAnExpiryNotice_EvenWhenAnUnrelatedAssignmentExpires()
    {
        var hotel = new Hotel();
        hotel.Restrict(PermissionNodes.Chat.SPEAK, temporary: false);
        hotel.Nodes[("command.kick", true)] = Node(
            "command.kick",
            true,
            hotel.Clock.Now.AddMinutes(1)
        );
        hotel.Resolve();
        hotel.Clock.Now = hotel.Clock.Now.AddMinutes(2);
        hotel.Nodes[(PermissionNodes.Chat.SPEAK, false)] = Node(PermissionNodes.Chat.SPEAK, true);
        hotel.Resolve();
        hotel.Notices.Delivered.Should().BeEmpty();
    }

    [Fact]
    public async Task OfflineExpiry_IsDropped_AndReconnectDoesNotReplayRestoration()
    {
        var hotel = new Hotel();
        hotel.Restrict(PermissionNodes.Chat.SPEAK);
        hotel.Restrict(PermissionNodes.TRADE);
        hotel.Resolve();
        hotel.Notices.Online = false;
        hotel.Clock.Now = hotel.Clock.Now.AddMinutes(2);
        hotel.Resolve();
        hotel.Notices.Attempts.Should().Be(1);
        hotel.Notices.Online = true;
        await hotel.Grain.NotifyActiveRestrictionsAsync(CancellationToken.None);
        hotel.Notices.Delivered.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginQuietlyResolvesExpiredRestrictions_WithoutRestorationBacklog()
    {
        var hotel = new Hotel();
        hotel.Restrict(PermissionNodes.Chat.SPEAK);
        hotel.Resolve();
        hotel.Clock.Now = hotel.Clock.Now.AddMinutes(2);
        await hotel.Grain.NotifyActiveRestrictionsAsync(CancellationToken.None);
        hotel.Notices.Delivered.Should().BeEmpty();
    }

    [Fact]
    public async Task ExplicitCleanupAfterExpiry_SuppressesItsOwnNotice_ButNotAnotherExpiredRestriction()
    {
        var hotel = new Hotel();
        hotel.Restrict(PermissionNodes.Chat.SPEAK);
        hotel.Restrict(PermissionNodes.TRADE);
        await using (var db = hotel.Db.CreateDbContext())
        {
            db.PlayerPermissionNodes.Add(
                new PlayerPermissionNodeEntity
                {
                    PlayerEntityId = 1,
                    Node = PermissionNodes.Chat.SPEAK,
                    Value = false,
                    IsTemporary = true,
                    ExpiresAt = hotel.Clock.Now.AddMinutes(1),
                }
            );
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        hotel.Resolve();
        hotel.Clock.Now = hotel.Clock.Now.AddMinutes(2);
        (
            await hotel.Grain.UnsetNodeAsync(
                PermissionNodes.Chat.SPEAK,
                temporary: true,
                actor: null,
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(PermissionChangeResultType.Changed);
        hotel.Notices.Delivered.Should().Equal("player.restriction.trade.restored");
    }

    [Fact]
    public void ExpiryOfInheritedGroupDenial_NotifiesOnlyWhenCurrentStateIsActuallyGranted()
    {
        var hotel = new Hotel();
        hotel.Nodes.Remove((PermissionNodes.TRADE, false));
        hotel.Groups(
            new PermissionGroupSnapshot
            {
                Id = 1,
                Name = PermissionGroupNames.DEFAULT,
                DisplayName = "Default",
                Weight = 0,
                ParentIds = [],
                Meta = [],
                Nodes =
                [
                    Node(PermissionNodes.TRADE, true),
                    Node(PermissionNodes.TRADE, false, hotel.Clock.Now.AddMinutes(1)),
                ],
            }
        );
        hotel.Resolve();
        hotel.Clock.Now = hotel.Clock.Now.AddMinutes(2);
        hotel.Resolve();
        hotel.Notices.Delivered.Should().Equal("player.restriction.trade.restored");
    }
}
