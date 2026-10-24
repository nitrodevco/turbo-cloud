using System.Text.Json;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Providers;

namespace Turbo.Tests.Support;

/// <summary>
/// A room as an official client capture entered it: the model of its FloorHeightMap, every floor
/// furni of its Objects (at the altitude, turn and stack height Habbo sent; size, stand, sit, lay
/// and stack-on from the furni data of its sprite), in a <see cref="WiredRoom"/>. Furni keep the
/// ids the fixture gave them (1..n); <see cref="HeightMap"/> is the stacking height map Habbo sent
/// on entry, tile by tile, as the client reads it (HeightMapMessageParser.as:93-108).
/// </summary>
public sealed class RecordedRoom
{
    private readonly Dictionary<int, JsonElement> _types;
    private readonly JsonElement[] _areaHides;

    public RecordedRoom(JsonElement entry, Dictionary<int, JsonElement> types)
    {
        _types = types;

        var modelData = RoomModelProvider.CleanModelString(entry.GetProperty("model").GetString()!);
        var compiled = RoomModelProvider.CompileModelFromString(modelData);
        var door = entry.GetProperty("door");

        Room = new WiredRoom(
            model: new RoomModelSnapshot
            {
                Id = 1,
                Name = "recorded",
                Model = modelData,
                DoorX = door[0].GetInt32(),
                DoorY = door[1].GetInt32(),
                DoorRotation = (Rotation)door[2].GetInt32(),
                Width = compiled.Width,
                Height = compiled.Height,
                Size = compiled.Width * compiled.Height,
                BaseHeights = compiled.Heights,
                BaseFlags = compiled.Flags,
            }
        );
        HeightMap = DecodeHeightMap(entry.GetProperty("heightMap").GetString()!, out var width);
        Width = width;
        _areaHides = [.. entry.GetProperty("areaHide").EnumerateArray()];

        foreach (var row in entry.GetProperty("items").EnumerateArray())
            Set(row, 0);
    }

    public WiredRoom Room { get; }

    public int Width { get; }

    /// <summary>The HeightMap Habbo sent on entry: -1 no tile, else the height × 256 and the stacking-blocked bit.</summary>
    public short[] HeightMap { get; }

    /// <summary>
    /// Whether a Room Area Hider that was on when the capture entered takes the tile out of the floor
    /// (FloorHeightMap's area hide data <c>[x, y, width, length, invert]</c>): the client gets no tile there.
    /// </summary>
    public bool IsHiddenByArea(int idx)
    {
        var (x, y) = (idx % Width, idx / Width);

        return _areaHides.Any(a =>
        {
            var inside =
                x >= a[0].GetInt32()
                && y >= a[1].GetInt32()
                && x < a[0].GetInt32() + a[2].GetInt32()
                && y < a[1].GetInt32() + a[3].GetInt32();

            return a[4].GetInt32() == 1 ? !inside : inside;
        });
    }

    /// <summary>
    /// A furni row <c>[id, sprite, x, y, z, dir, stackHeight]</c> from <paramref name="at"/>
    /// on: put in the room, or where the row says if it is there already.
    /// </summary>
    public void Set(JsonElement row, int at)
    {
        var id = row[at].GetInt32();
        var (x, y) = (row[at + 2].GetInt32(), row[at + 3].GetInt32());
        var z = Altitude.FromValue(row[at + 4].GetDouble());
        var rotation = (Rotation)row[at + 5].GetInt32();
        var map = Room.Map;

        if (Room.Harness.State.ItemsById.TryGetValue(id, out var known))
        {
            map.MoveFloorItem((IRoomFloorItem)known, map.ToIdx(x, y), z, rotation);

            return;
        }

        var type = _types[row[at + 1].GetInt32()];
        var item = new RoomFloorItem
        {
            ObjectId = id,
            OwnerId = 1,
            OwnerName = "recorded",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = row[at + 1].GetInt32(),
                SpriteId = row[at + 1].GetInt32(),
                Name = type[0].GetString()!,
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "default_floor",
                TotalStates = 2,
                Width = type[1].GetInt32(),
                Length = type[2].GetInt32(),
                StackHeight = Altitude.FromValue(row[at + 6].GetDouble()),
                CanWalk = type[3].GetInt32() == 1,
                CanSit = type[4].GetInt32() == 1,
                CanLay = type[5].GetInt32() == 1,
                CanStack = type[6].GetInt32() == 1,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Everybody,
                ExtraData = null,
            },
        };

        // No state: the fixture leaves it out (it can hold player text) and heights do not read it.
        item.SetExtraData(null);
        item.SetPosition(x, y);
        item.SetPositionZ(z);
        item.SetRotation(rotation);
        item.SetLogic(
            new FurnitureFloorLogic(
                Room.StuffData,
                new RoomFloorItemContext(Room.Harness.Room, item)
            )
        );
        Room.Harness.State.ItemsById.Add(id, item);
        map.AddItem(item);
    }

    /// <summary>A furni the room no longer has.</summary>
    public void Remove(int id)
    {
        if (!Room.Harness.State.ItemsById.TryGetValue(id, out var item))
            return;

        Room.Map.RemoveItem(item);
        Room.Harness.State.ItemsById.Remove(id);
    }

    /// <summary>The fixture's recordings, and the furni data of every sprite they hold.</summary>
    public static (List<JsonElement> Recordings, Dictionary<int, JsonElement> Types) Load(
        string resource
    )
    {
        using var stream = typeof(RecordedRoom).Assembly.GetManifestResourceStream(resource)!;
        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement.Clone();

        return (
            [.. root.GetProperty("recordings").EnumerateArray()],
            root.GetProperty("types")
                .EnumerateObject()
                .ToDictionary(p => int.Parse(p.Name), p => p.Value)
        );
    }

    private static short[] DecodeHeightMap(string hex, out int width)
    {
        var bytes = Convert.FromHexString(hex);
        width = ReadInt(bytes, 0);
        var tiles = new short[ReadInt(bytes, 4)];

        for (var i = 0; i < tiles.Length; i++)
            tiles[i] = (short)((bytes[8 + i * 2] << 8) | bytes[9 + i * 2]);

        return tiles;
    }

    private static int ReadInt(byte[] b, int at) =>
        (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];
}
