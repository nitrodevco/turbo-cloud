using Turbo.Contracts.Hotel;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Testing;

namespace Turbo.Tests.Hotel;

/// <summary>The contract scenarios on <see cref="FakeHotel"/>, the hotel plugins' tests use.</summary>
public sealed class FakeHotelContractTests : HotelContract
{
    protected override IHotelWorld NewWorld() => new FakeWorld();

    private sealed class FakeWorld : IHotelWorld
    {
        private readonly FakeHotel _hotel = new();

        public IHotel Hotel => _hotel;

        public PlayerId AddPlayer(
            string name,
            bool online = false,
            bool systemAccount = false,
            int respects = 0
        )
        {
            var player = _hotel.Players.Add(name, online);
            player.IsSystemAccount = systemAccount;
            player.Respects = respects;

            return player;
        }

        public void SetBalance(PlayerId player, string currency, int amount) =>
            _hotel.Wallet.Set(player, currency, amount);

        public RoomId AddRoom(string name, PlayerId owner, bool isPublic = false) =>
            _hotel.Rooms.Add(name, Player(owner), isPublic);

        public void Enter(RoomId room, PlayerId player) =>
            _hotel.Rooms.Enter(_hotel.Rooms.All.Single(x => x.Id == room), Player(player));

        public int AddFurniture(string name) => _hotel.Inventory.AddFurniture(name).DefinitionId;

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private FakePlayer Player(PlayerId id) => _hotel.Players.All.Single(x => x.Id == id);
    }
}
