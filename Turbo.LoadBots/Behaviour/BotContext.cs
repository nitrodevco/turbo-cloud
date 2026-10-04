using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.LoadBots.Client;
using Turbo.LoadBots.Knowledge;
using Turbo.LoadBots.Metrics;
using Turbo.LoadBots.Protocol.Decoders;
using Turbo.Primitives.Navigator;

namespace Turbo.LoadBots.Behaviour;

/// <summary>
/// One bot's mind: its session, its own random stream and what it remembers between
/// activities (its rooms, where their doors are). The activities share it.
/// </summary>
public sealed class BotContext(
    BotClient client,
    Persona persona,
    Random random,
    LoadBotOptions options,
    HotelKnowledge knowledge,
    BotMetrics metrics,
    SharedWorld world,
    ILogger logger
)
{
    private readonly Dictionary<int, RoomPurpose> _ownRooms = [];
    private readonly Dictionary<int, (int X, int Y)> _doors = [];
    private bool _ownRoomsLoaded;

    public BotClient Client { get; } = client;
    public Persona Persona { get; } = persona;
    public Random Random { get; } = random;
    public LoadBotOptions Options { get; } = options;
    public HotelKnowledge Knowledge { get; } = knowledge;
    public BotMetrics Metrics { get; } = metrics;
    public SharedWorld World { get; } = world;
    public ILogger Logger { get; } = logger;

    /// <summary>The room the bot is standing in, when it is in one.</summary>
    public RoomView? Room => Client.Room is { Entered: true } room ? room : null;

    public T Pick<T>(IReadOnlyList<T> items) => items[Random.Next(items.Count)];

    public bool Chance(double probability) => Random.NextDouble() < probability;

    public Task ThinkAsync(CancellationToken ct) =>
        Task.Delay(Random.Next(Options.Run.ThinkTimeMinMs, Options.Run.ThinkTimeMaxMs + 1), ct);

    /// <summary>The door tile of a room the bot entered: where its avatar first stood.</summary>
    public (int X, int Y)? Door(int roomId) =>
        _doors.TryGetValue(roomId, out var door) ? door : null;

    /// <summary>Enters a room (if not already in it) and remembers where the door is.</summary>
    public async Task<bool> EnterAsync(int roomId, CancellationToken ct)
    {
        if (Room?.RoomId == roomId)
            return true;

        if (!await Client.EnterRoomAsync(roomId, ct))
            return false;

        var room = Client.Room;

        if (room?.Position(room.SelfObjectId) is { } door && !_doors.ContainsKey(roomId))
            _doors[roomId] = door;

        return true;
    }

    /// <summary>
    /// A room of the bot's own for <paramref name="purpose"/>: one it already made for it, or a
    /// new one while it owns fewer than the configured number, or any of its rooms after that.
    /// </summary>
    public async Task<int?> OwnRoomAsync(RoomPurpose purpose, CancellationToken ct)
    {
        await LoadOwnRoomsAsync(ct);

        var forPurpose = _ownRooms.Where(x => x.Value == purpose).Select(x => x.Key).ToList();

        // A game or a lab is one room the bot keeps going back to; a builder now and then
        // starts another room while it has rooms to spare.
        var wantsAnother =
            purpose is RoomPurpose.Decor or RoomPurpose.Architecture
            && _ownRooms.Count < Options.Run.MaxRoomsPerBot
            && Chance(0.15);

        if (forPurpose.Count > 0 && !wantsAnother)
            return Pick(forPurpose);

        if (_ownRooms.Count >= Options.Run.MaxRoomsPerBot)
        {
            // Out of fresh rooms: repurpose one of the others.
            var reused = Pick(_ownRooms.Keys.ToList());
            _ownRooms[reused] = purpose;

            return reused;
        }

        var name = string.Create(
            CultureInfo.InvariantCulture,
            $"{Client.Name} {purpose} {_ownRooms.Count + 1}"
        );
        var roomId = await Client.CreateRoomAsync(name, Pick(Knowledge.RoomModels), 25, ct);

        if (roomId is { } id)
            _ownRooms[id] = purpose;

        return roomId;
    }

    /// <summary>The bot's own rooms, found the way a player finds them: "my rooms".</summary>
    private async Task LoadOwnRoomsAsync(CancellationToken ct)
    {
        if (_ownRoomsLoaded)
            return;

        var listed = await Client.SearchRoomsAsync(
            NavigatorSearchCodes.MYWORLD_VIEW,
            string.Empty,
            ct
        );

        if (listed is null)
            return;

        foreach (var room in listed.Where(x => x.OwnerId == Client.PlayerId))
            _ownRooms.TryAdd(room.RoomId, PurposeFromName(room));

        _ownRoomsLoaded = true;
    }

    private static RoomPurpose PurposeFromName(RoomListing room)
    {
        foreach (var purpose in Enum.GetValues<RoomPurpose>())
        {
            if (room.Name.Contains(purpose.ToString(), StringComparison.Ordinal))
                return purpose;
        }

        return RoomPurpose.Decor;
    }

    /// <summary>A tile of the current room the bot can walk to from where it stands, chosen at random.</summary>
    public (int X, int Y)? RandomWalkableTile()
    {
        var room = Room;
        var tiles = room?.ReachableTiles(Knowledge, room.SelfObjectId);

        return tiles is { Count: > 0 } ? Pick(tiles) : null;
    }

    /// <summary>
    /// A furni of this offer the bot owns and has not placed, bought first if it has none.
    /// </summary>
    public async Task<InventoryItem?> ObtainAsync(FurniOffer offer, CancellationToken ct)
    {
        await Client.RefreshInventoryIfInvalidAsync(ct);

        return Client.OwnedUnplaced(offer.Definition.SpriteId) ?? await Client.BuyAsync(offer, ct);
    }

    /// <summary>
    /// Puts a furni from this offer somewhere free in the current room. Returns the room object.
    /// </summary>
    public async Task<FloorItem?> PlaceSomewhereAsync(FurniOffer offer, CancellationToken ct)
    {
        var room = Room;

        if (room is null)
            return null;

        var rotation = Pick(ROTATIONS);
        var (width, length) = RoomView.Footprint(offer.Definition, rotation);
        var spots = room.FreeSpots(width, length, Knowledge, Door(room.RoomId));

        if (spots.Count == 0)
        {
            Metrics.Count("furni.place.no_room");

            return null;
        }

        var item = await ObtainAsync(offer, ct);

        if (item is null)
            return null;

        var (x, y) = Pick(spots);

        return await Client.PlaceAsync(item, x, y, rotation, ct);
    }

    private static readonly int[] ROTATIONS = [0, 2, 4, 6];
}
