using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Rooms;

namespace Turbo.Testing;

/// <summary>The navigator of a <see cref="FakeHotel"/>.</summary>
public sealed class FakeNavigator : IHotelNavigator
{
    internal FakeNavigator(FakeHotel hotel) => Public = new FakePublicRooms(hotel);

    public FakePublicRooms Public { get; }

    IHotelPublicRooms IHotelNavigator.Public => Public;
}

/// <summary>A category of a <see cref="FakeHotel"/>'s Public tab; a test changes it as staff would.</summary>
public sealed class FakePublicCategory
{
    internal FakePublicCategory(int id, string? key, string name)
    {
        Id = id;
        Key = key;
        Name = name;
    }

    public int Id { get; }

    /// <summary>The plugin's key; null for one staff made.</summary>
    public string? Key { get; }

    public string Name { get; set; }

    public bool Visible { get; set; } = true;

    public bool ForceClosed { get; set; }

    public bool Thumbnails { get; set; } = true;

    public List<RoomId> Rooms { get; } = [];

    public override string ToString() => $"{Name} ({Key ?? "staff"})";
}

/// <summary>
/// The Public tab of a <see cref="FakeHotel"/>, with the host's rules: a plugin changes only the
/// categories it made, by key, and <see cref="EnsureCategoryAsync"/> makes one once. A room in a
/// shown category is public (<see cref="IHotelRooms.PublicIdsAsync"/>).
/// </summary>
public sealed class FakePublicRooms : IHotelPublicRooms
{
    private readonly FakeHotel _hotel;
    private readonly List<FakePublicCategory> _categories = [];
    private int _nextId = 1;

    internal FakePublicRooms(FakeHotel hotel) => _hotel = hotel;

    /// <summary>The categories in the tab's order.</summary>
    public IReadOnlyList<FakePublicCategory> All => _categories;

    /// <summary>A category as staff make one in the panel: no key, last in the tab.</summary>
    public FakePublicCategory AddStaffCategory(string name, params RoomId[] rooms)
    {
        var category = new FakePublicCategory(_nextId++, null, name);

        category.Rooms.AddRange(rooms);
        _categories.Add(category);

        return category;
    }

    /// <summary>The plugin's category under the key; fails the test when there is none.</summary>
    public FakePublicCategory Category(string key) =>
        _categories.FirstOrDefault(x => x.Key == key)
        ?? throw new HotelAssertionException(
            $"The fake hotel has no public category {key}; it has {FakeHotelText.List(_categories)}."
        );

    public Task<IReadOnlyList<HotelPublicCategory>> CategoriesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HotelPublicCategory>>([.. _categories.Select(ToHotel)]);

    public Task<HotelPublicCategory> EnsureCategoryAsync(
        string key,
        HotelPublicCategoryDefaults defaults,
        CancellationToken ct
    )
    {
        HotelPublicKey.ThrowIfInvalid(key);
        ArgumentNullException.ThrowIfNull(defaults);

        if (_categories.FirstOrDefault(x => x.Key == key) is { } existing)
            return Task.FromResult(ToHotel(existing));

        if (string.IsNullOrWhiteSpace(defaults.Name))
            throw new ArgumentException("A category needs a name.", nameof(defaults));

        var rooms = defaults.Rooms ?? [];

        foreach (var room in rooms)
            ThrowIfNoRoom(room);

        var category = new FakePublicCategory(_nextId++, key, defaults.Name.Trim())
        {
            Visible = defaults.Visible,
            ForceClosed = defaults.ForceClosed,
            Thumbnails = defaults.Thumbnails,
        };

        category.Rooms.AddRange(rooms.Distinct());
        _categories.Add(category);

        return Task.FromResult(ToHotel(category));
    }

    public Task<bool> AddRoomAsync(string key, RoomId room, bool first, CancellationToken ct)
    {
        var category = Own(key);

        ThrowIfNoRoom(room);

        if (category.Rooms.Contains(room))
            return Task.FromResult(false);

        category.Rooms.Insert(first ? 0 : category.Rooms.Count, room);

        return Task.FromResult(true);
    }

    public Task<bool> RemoveRoomAsync(string key, RoomId room, CancellationToken ct) =>
        Task.FromResult(Own(key).Rooms.Remove(room));

    public Task OrderRoomsAsync(string key, IReadOnlyList<RoomId> rooms, CancellationToken ct)
    {
        var category = Own(key);

        if (
            rooms.Count != category.Rooms.Count
            || rooms.Distinct().Count() != rooms.Count
            || !rooms.All(category.Rooms.Contains)
        )
            throw new ArgumentException("Name every room once, in the new order.", nameof(rooms));

        category.Rooms.Clear();
        category.Rooms.AddRange(rooms);

        return Task.CompletedTask;
    }

    internal bool Lists(RoomId room) => _categories.Any(x => x.Visible && x.Rooms.Contains(room));

    private FakePublicCategory Own(string key)
    {
        HotelPublicKey.ThrowIfInvalid(key);

        return _categories.FirstOrDefault(x => x.Key == key)
            ?? throw new ArgumentException(
                $"There is no public category {key}: make it with EnsureCategoryAsync.",
                nameof(key)
            );
    }

    private void ThrowIfNoRoom(RoomId room)
    {
        if (!_hotel.Rooms.All.Any(x => x.Id == room))
            throw new ArgumentException($"There is no room {room.Value}.", nameof(room));
    }

    private static HotelPublicCategory ToHotel(FakePublicCategory x) =>
        new(x.Id, x.Key, x.Name, x.Visible, x.ForceClosed, x.Thumbnails, false, [.. x.Rooms]);
}
