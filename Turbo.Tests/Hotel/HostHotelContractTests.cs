using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Commands;
using Turbo.Contracts.Hotel;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Furniture;
using Turbo.Furniture.Configuration;
using Turbo.Inventory.Grains.Badges;
using Turbo.Main.Hotel;
using Turbo.Navigator;
using Turbo.Players.Grains;
using Turbo.Players.Providers;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Hotel;

/// <summary>
/// The contract scenarios on the host's hotel API as the server builds it
/// (<see cref="HotelServiceCollectionExtensions.AddTurboHotel"/>): its reads over a real database,
/// the real wallet, inventory and badge grains, and the real currency types and furni definitions.
/// Who is online, who is in which room and the navigator's public list are the fakes' answers:
/// they are the session gateway's, the room directory's and the navigator's, not the hotel API's.
/// </summary>
public sealed class HostHotelContractTests : HotelContract
{
    protected override IHotelWorld NewWorld() => new HostWorld();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A plugin's change to the Public tab is in the command log as the plugin's, beside the panel's.</summary>
    [Fact]
    public async Task A_plugins_public_tab_change_is_logged_as_the_plugins()
    {
        await using var world = new HostWorld();
        var owner = world.AddPlayer("Owner");
        var lobby = world.AddRoom("Lobby", owner);
        await world.StartAsync(Ct);
        var tab = world.Hotel.Navigator.Public;

        var made = await tab.EnsureCategoryAsync("demo:games", new("Games"), Ct);
        await tab.EnsureCategoryAsync("demo:games", new("Games"), Ct);
        await tab.AddRoomAsync("demo:games", lobby, first: true, Ct);

        world
            .CommandLog()
            .Should()
            .Equal(
                ("navigator", 0, $"public category add {made.Id} Games demo:games", "plugin"),
                (
                    "navigator",
                    0,
                    $"public room add {made.Id} {lobby.Value} first demo:games",
                    "plugin"
                )
            );
    }

    private sealed class HostWorld : IHotelWorld
    {
        private readonly FurnitureWorld _world = new();
        private readonly HashSet<PlayerId> _online = [];
        private readonly Dictionary<RoomId, HashSet<PlayerId>> _inRoom = [];
        private readonly List<(RoomId Id, string Name, PlayerId Owner)> _public = [];
        private readonly List<PlayerId> _players = [];
        private IHotel? _hotel;
        private int _balances;
        private HotelAvailabilitySnapshot _availability = HotelAvailabilitySnapshot.Open;

        public HostWorld()
        {
            Insert(1, "credits", CurrencyType.Credits, null);
            Insert(2, "duckets", CurrencyType.ActivityPoints, 0);
            Insert(3, "diamonds", CurrencyType.ActivityPoints, 5);

            var fakes = _world.Fakes;

            fakes.Handlers[nameof(ISessionGateway.GetOnlinePlayerIds)] = _ =>
                (IReadOnlyCollection<PlayerId>)[.. _online];
            fakes.Handlers[nameof(IPlayerPresenceGrain.TrySendComposerAsync)] = call =>
                Task.FromResult(_online.Contains(new PlayerId((int)(long)call.Key!)));
            fakes.Handlers[nameof(IPlayerPresenceGrain.GetActiveRoomAsync)] = call =>
                Task.FromResult(
                    new RoomPointerSnapshot
                    {
                        RoomId = _inRoom
                            .Where(x => x.Value.Contains(new PlayerId((int)(long)call.Key!)))
                            .Select(x => x.Key)
                            .DefaultIfEmpty(new RoomId(-1))
                            .First(),
                        ActiveSinceUtc = DateTime.UtcNow,
                    }
                );
            fakes.Handlers[nameof(IRoomDirectoryGrain.GetRoomPopulationAsync)] = call =>
                Task.FromResult(In((RoomId)call.Args[0]!).Count);
            fakes.Handlers[nameof(IRoomDirectoryGrain.GetRoomPlayersAsync)] = call =>
                Task.FromResult(ImmutableArray.CreateRange(In((RoomId)call.Args[0]!).Order()));
            fakes.Handlers["WhisperToPlayerAsync"] = call =>
                Task.FromResult(
                    In(new RoomId((int)(long)call.Key!)).Contains((PlayerId)call.Args[0]!)
                );
            fakes.Handlers[nameof(INavigatorService.GetOfficialRoomsAsync)] = _ =>
                Task.FromResult(ImmutableArray.CreateRange(_public.Select(Listed)));
            fakes.Handlers[nameof(IPlayerNoticeService.SendAsync)] = call =>
                Task.FromResult(
                    _online.Contains((PlayerId)call.Args[0]!)
                        ? PlayerNoticeDelivery.Sent
                        : PlayerNoticeDelivery.Offline
                );
            fakes.Handlers[nameof(IBadgeDirectoryGrain.GetInfoAsync)] = call =>
                Task.FromResult(
                    ImmutableArray.CreateRange(
                        ((ImmutableArray<string>)call.Args[0]!).Select(code => new BadgeInfoSnapshot
                        {
                            BadgeCode = code,
                            OwnerCount = 1,
                            Rarity = BadgeRarityType.Common,
                        })
                    )
                );
            fakes.Handlers[nameof(IBadgeDirectoryGrain.OnBadgeGrantedAsync)] = call =>
                Task.FromResult(
                    new BadgeInfoSnapshot
                    {
                        BadgeCode = (string)call.Args[0]!,
                        OwnerCount = 1,
                        Rarity = BadgeRarityType.Common,
                    }
                );
            fakes.Handlers[nameof(IBadgeDirectoryGrain.GetRequestableBadgeAsync)] = _ => null;
            fakes.Handlers["get_" + nameof(IHotelAvailability.Current)] = call =>
                call.Interface == typeof(IHotelAvailability) ? _availability : Fakes.NotHandled;
        }

        public IHotel Hotel => _hotel ?? throw new InvalidOperationException("Start it first.");

        public PlayerId AddPlayer(
            string name,
            bool online = false,
            bool systemAccount = false,
            int respects = 0,
            DateTime? lastLogin = null
        )
        {
            var id = new PlayerId(_players.Count + 1);

            _world.Db.Insert(
                new PlayerEntity
                {
                    Id = id.Value,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                    IsSystemAccount = systemAccount,
                    RespectPoints = respects,
                    LastLoginAt = lastLogin,
                }
            );
            _players.Add(id);

            if (online)
                _online.Add(id);

            return id;
        }

        public void SetBalance(PlayerId player, string currency, int amount) =>
            _world.Db.Insert(
                new PlayerCurrencyEntity
                {
                    Id = ++_balances,
                    PlayerEntityId = player.Value,
                    CurrencyTypeEntityId = currency switch
                    {
                        "credits" => 1,
                        "duckets" => 2,
                        _ => 3,
                    },
                    Amount = amount,
                }
            );

        public RoomId AddRoom(string name, PlayerId owner, bool isPublic = false)
        {
            var id = new RoomId(_inRoom.Count + 1);

            _world.AddRoom(id.Value, owner.Value);

            using (var db = _world.Db.CreateDbContext())
            {
                db.Rooms.Single(x => x.Id == id.Value).Name = name;
                db.SaveChanges();
            }

            _inRoom[id] = [];

            if (isPublic)
                _public.Add((id, name, owner));

            return id;
        }

        public void Enter(RoomId room, PlayerId player) => _inRoom[room].Add(player);

        public void GiveRespect(PlayerId from, PlayerId to)
        {
            using var db = _world.Db.CreateDbContext();

            db.HumanRespectOperations.Add(
                new HumanRespectOperationEntity
                {
                    OperationId = Guid.NewGuid().ToString("N"),
                    ActorId = from.Value,
                    TargetId = to.Value,
                    Completed = true,
                }
            );
            // One that ran out of respects: not given.
            db.HumanRespectOperations.Add(
                new HumanRespectOperationEntity
                {
                    OperationId = Guid.NewGuid().ToString("N"),
                    ActorId = from.Value,
                    TargetId = to.Value,
                    Rejected = true,
                }
            );
            db.Players.Single(x => x.Id == to.Value).RespectPoints++;
            db.SaveChanges();
        }

        public void SetOnlineTime(PlayerId player, TimeSpan total)
        {
            using var db = _world.Db.CreateDbContext();

            db.Players.Single(x => x.Id == player.Value).OnlineMilliseconds = (long)
                total.TotalMilliseconds;
            db.SaveChanges();
        }

        public void CloseForMaintenance(string reason) =>
            _availability = new HotelAvailabilitySnapshot(
                HotelAvailabilityPhase.Maintenance,
                null,
                reason
            );

        public async Task StaffRenamePublicCategoryAsync(
            int id,
            string name,
            CancellationToken ct
        ) =>
            await Editor()
                .SaveCategoryAsync(
                    id,
                    new PublicCategoryChange(name, Visible: false),
                    NavigatorPublicCategoryKind.Manual,
                    null,
                    ct
                );

        /// <summary>The command log's rows, as the panel's Command log page lists them.</summary>
        public List<(string Command, int Player, string Arguments, string? Source)> CommandLog()
        {
            using var db = _world.Db.CreateDbContext();

            return
            [
                .. db
                    .CommandLogs.OrderBy(x => x.Id)
                    .Select(x => new
                    {
                        x.Command,
                        x.PlayerEntityId,
                        x.Arguments,
                        x.Source,
                    })
                    .AsEnumerable()
                    .Select(x => (x.Command, x.PlayerEntityId, x.Arguments, x.Source)),
            ];
        }

        private PublicRoomEditor Editor() =>
            new(
                _world.Db,
                _world.Fakes.Create<IGrainFactory>(),
                NullLogger<IPublicRoomEditor>.Instance
            );

        public int AddFurniture(string name)
        {
            var id = 100 + _players.Count + _inRoom.Count;

            _world.AddDefinition(id, name, "default");

            return id;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            var db = _world.Db;
            var fakes = _world.Fakes;
            var currencies = new CurrencyTypeProvider(
                db,
                NullLogger<ICurrencyTypeProvider>.Instance
            );
            var definitions = new FurnitureDefinitionProvider(
                Options.Create(new FurnitureConfig()),
                db,
                NullLogger<IFurnitureDefinitionProvider>.Instance
            );

            await currencies.ReloadAsync(ct);
            await definitions.ReloadAsync(ct);

            foreach (var player in _players)
            {
                var wallet = GrainHarness.Create<PlayerWalletGrain>(fakes, db, player.Value);
                GrainHarness.Replace<ICurrencyTypeProvider>(wallet, currencies);
                GrainHarness.Replace<IPlayerNoticeService>(
                    wallet,
                    fakes.Create<IPlayerNoticeService>()
                );
                await ((Grain)wallet).OnActivateAsync(ct);
                fakes.Instances[(typeof(IPlayerWalletGrain), (long)player.Value)] = wallet;

                var badges = GrainHarness.Create<PlayerBadgeGrain>(fakes, db, player.Value);
                await ((Grain)badges).OnActivateAsync(ct);
                fakes.Instances[(typeof(IPlayerBadgeGrain), (long)player.Value)] = badges;

                fakes.Instances[(typeof(IInventoryGrain), (long)player.Value)] =
                    await _world.NewInventoryAsync(player.Value, ct);
            }

            _hotel = new ServiceCollection()
                .AddSingleton<Microsoft.EntityFrameworkCore.IDbContextFactory<Turbo.Database.Context.TurboDbContext>>(
                    db
                )
                .AddSingleton(fakes.Create<IGrainFactory>())
                .AddSingleton(fakes.Create<ISessionGateway>())
                .AddSingleton(fakes.Create<INavigatorService>())
                .AddSingleton(fakes.Create<IPlayerNoticeService>())
                .AddSingleton(fakes.Create<ICatalogSnapshotProvider<NormalCatalog>>())
                .AddSingleton(fakes.Create<ICatalogEditService>())
                .AddSingleton(fakes.Create<IExpiringPageService>())
                .AddSingleton(fakes.Create<IHotelAvailability>())
                .AddSingleton<ICurrencyTypeProvider>(currencies)
                .AddSingleton<IFurnitureDefinitionProvider>(definitions)
                .AddSingleton(new TestEventBus().System)
                .AddSingleton<IPublicRoomEditor>(Editor())
                .AddSingleton<AdminActionLog>()
                .AddLogging()
                .AddTurboHotel()
                .BuildServiceProvider()
                .GetRequiredService<IHotel>();
        }

        public ValueTask DisposeAsync()
        {
            _world.Dispose();

            return ValueTask.CompletedTask;
        }

        private HashSet<PlayerId> In(RoomId room) => _inRoom.GetValueOrDefault(room) ?? [];

        private void Insert(int id, string name, CurrencyType type, int? pointType) =>
            _world.Db.Insert(
                new CurrencyTypeEntity
                {
                    Id = id,
                    Name = name,
                    CurrencyType = type,
                    ActivityPointType = pointType,
                    Enabled = true,
                }
            );

        private static NavigatorSearchResultSnapshot Listed(
            (RoomId Id, string Name, PlayerId Owner) room
        ) =>
            new()
            {
                RoomId = room.Id,
                Name = room.Name,
                Description = string.Empty,
                OwnerId = room.Owner,
                OwnerName = string.Empty,
                Population = 0,
                LastUpdatedUtc = DateTime.UtcNow,
                DoorMode = RoomDoorModeType.Open,
                PlayersMax = 25,
                TradeType = RoomTradeModeType.Disabled,
                Score = 0,
                Ranking = 0,
                CategoryId = -1,
                Tags = [],
                AllowBlocking = false,
                AllowPets = false,
                AllowPetsEat = false,
            };
    }
}
