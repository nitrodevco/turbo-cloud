using System;
using System.Collections.Generic;
using System.Globalization;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.LoadBots.Protocol.Decoders;

/// <summary>An avatar in the room: a player, pet or bot.</summary>
public sealed record RoomAvatar(
    int WebId,
    string Name,
    int ObjectId,
    int X,
    int Y,
    double Z,
    int BodyRotation,
    RoomObjectType Type
);

/// <summary>Where an avatar is now, and what it is doing.</summary>
public sealed record AvatarStatus(int ObjectId, int X, int Y, double Z, string Status);

/// <summary>A floor item in the room.</summary>
public sealed record FloorItem(
    int ObjectId,
    int SpriteId,
    int X,
    int Y,
    int Rotation,
    double Z,
    string State,
    int OwnerId
);

/// <summary>
/// The floor as the server sent it: -1 for no tile, else height × 256 with bit 14 set where
/// furniture blocks stacking.
/// </summary>
public sealed record HeightMap(int Width, int Length, short[] Heights)
{
    private const short STACKING_BLOCKED = 1 << 14;

    /// <summary>A tile to walk to: part of the room, and not under furniture that blocks stacking.</summary>
    public bool IsTile(int x, int y) =>
        x >= 0
        && y >= 0
        && x < Width
        && y < Length
        && Heights[y * Width + x] >= 0
        && (Heights[y * Width + x] & STACKING_BLOCKED) == 0;
}

/// <summary>The decoded floor plan: the model text and the camera fields after it.</summary>
public sealed record FloorPlan(bool Small, int FixedWallsHeight, string ModelData);

/// <summary>A chat line, said, shouted or whispered.</summary>
public sealed record ChatLine(int ObjectId, string Text, int StyleId);

/// <summary>
/// Decoders for the room engine messages, mirroring
/// <c>Turbo.Revisions/Revision20260909/Serializers/Room/Engine</c>.
/// </summary>
public static class RoomDecoders
{
    public static List<RoomAvatar> Users(PacketReader reader)
    {
        var count = reader.Count();
        var avatars = new List<RoomAvatar>(count);

        for (var i = 0; i < count; i++)
            avatars.Add(Avatar(reader));

        return avatars;
    }

    private static RoomAvatar Avatar(PacketReader reader)
    {
        var webId = reader.Int();
        var name = reader.String();
        _ = reader.String(); // motto
        _ = reader.String(); // figure
        var objectId = reader.Int();
        var x = reader.Int();
        var y = reader.Int();
        var z = reader.NumberString();
        var bodyRotation = reader.Int();
        var type = (RoomObjectType)reader.Int();

        switch (type)
        {
            case RoomObjectType.Player:
                _ = reader.String(); // gender
                _ = reader.Int(); // group id
                _ = reader.Int(); // group status
                _ = reader.String(); // group name
                _ = reader.String(); // swim figure
                _ = reader.Int(); // activity points
                _ = reader.Bool(); // moderator
                _ = reader.Int(); // badges rank
                break;
            case RoomObjectType.Pet:
                _ = reader.Int();
                _ = reader.Int();
                _ = reader.String();
                _ = reader.Int();
                for (var i = 0; i < 6; i++)
                    _ = reader.Bool();
                _ = reader.Int();
                _ = reader.String();
                break;
            case RoomObjectType.Bot:
                _ = reader.String();
                _ = reader.Int();
                _ = reader.String();
                var skills = reader.Count();
                for (var i = 0; i < skills; i++)
                    _ = reader.Short();
                break;
            default:
                break;
        }

        return new RoomAvatar(webId, name, objectId, x, y, z, bodyRotation, type);
    }

    public static List<AvatarStatus> UserUpdate(PacketReader reader)
    {
        var count = reader.Count();
        var statuses = new List<AvatarStatus>(count);

        for (var i = 0; i < count; i++)
        {
            var objectId = reader.Int();
            var x = reader.Int();
            var y = reader.Int();
            var z = reader.NumberString();
            _ = reader.Int(); // head rotation
            _ = reader.Int(); // body rotation
            _ = reader.Int(); // jump power
            var status = reader.String();

            statuses.Add(new AvatarStatus(objectId, x, y, z, status));
        }

        return statuses;
    }

    public static int UserRemove(PacketReader reader) =>
        int.Parse(reader.String(), NumberStyles.Integer, CultureInfo.InvariantCulture);

    public static List<FloorItem> Objects(PacketReader reader)
    {
        SkipOwnerNames(reader);

        var count = reader.Count();
        var items = new List<FloorItem>(count);

        for (var i = 0; i < count; i++)
            items.Add(FloorItem(reader));

        return items;
    }

    /// <summary>ObjectAdd: a floor item followed by its owner's name.</summary>
    public static FloorItem ObjectAdd(PacketReader reader)
    {
        var item = FloorItem(reader);
        _ = reader.String(); // owner name

        return item;
    }

    public static FloorItem ObjectUpdate(PacketReader reader) => FloorItem(reader);

    public static int ObjectRemove(PacketReader reader) =>
        int.Parse(reader.String(), NumberStyles.Integer, CultureInfo.InvariantCulture);

    public static FloorItem FloorItem(PacketReader reader)
    {
        var objectId = reader.Int();
        var spriteId = reader.Int();
        var x = reader.Int();
        var y = reader.Int();
        var rotation = reader.Int();
        var z = reader.NumberString();
        _ = reader.String(); // stack height
        _ = reader.Int(); // extra
        var state = StuffData(reader);
        _ = reader.Int(); // expiration
        _ = reader.Int(); // usage policy
        var ownerId = reader.Int();

        if (spriteId < 0)
            _ = reader.String(); // static class

        return new FloorItem(objectId, spriteId, x, y, rotation, z, state, ownerId);
    }

    /// <summary>
    /// Reads stuff data (<c>StuffDataSnapshotSerializer</c>) and returns its state as text: the
    /// legacy string, or the first value of the other shapes.
    /// </summary>
    public static string StuffData(PacketReader reader)
    {
        // The low byte is the shape, the byte above it the flags.
        var bitmask = reader.Int();
        var type = (StuffDataType)(bitmask & byte.MaxValue);
        var state = string.Empty;

        switch (type)
        {
            case StuffDataType.LegacyKey:
                state = reader.String();
                break;
            case StuffDataType.MapKey:
                var entries = reader.Count();
                for (var i = 0; i < entries; i++)
                {
                    var key = reader.String();
                    var value = reader.String();

                    if (key == "state")
                        state = value;
                }
                break;
            case StuffDataType.StringKey:
                var strings = reader.Strings();
                state = strings.Length > 0 ? strings[0] : string.Empty;
                break;
            case StuffDataType.VoteKey:
                state = reader.String();
                _ = reader.Int();
                break;
            case StuffDataType.EmptyKey:
                break;
            case StuffDataType.CrackableKey:
                state = reader.String();
                _ = reader.Int(); // hits
                _ = reader.Int(); // target
                break;
            case StuffDataType.NumberKey:
                var numbers = reader.Ints();
                state =
                    numbers.Length > 0
                        ? numbers[0].ToString(CultureInfo.InvariantCulture)
                        : string.Empty;
                break;
            case StuffDataType.HighscoreKey:
                state = reader.String();
                _ = reader.Int(); // score type
                _ = reader.Int(); // clear type
                var scores = reader.Count();
                for (var i = 0; i < scores; i++)
                {
                    _ = reader.Int();
                    _ = reader.Strings();
                }
                break;
            default:
                throw new FormatException($"Unknown stuff data type {type}.");
        }

        if ((bitmask & (int)StuffDataFlags.Unique) != 0)
        {
            _ = reader.Int();
            _ = reader.Int();
        }

        return state;
    }

    public static HeightMap HeightMap(PacketReader reader)
    {
        var width = reader.Int();
        var size = reader.Int();
        var heights = new short[size];

        for (var i = 0; i < size; i++)
            heights[i] = reader.Short();

        return new HeightMap(width, width == 0 ? 0 : size / width, heights);
    }

    /// <summary>HeightMapUpdate: a byte count, then x, y bytes and the encoded height.</summary>
    public static List<(int X, int Y, short Height)> HeightMapUpdate(PacketReader reader)
    {
        var count = reader.Byte();
        var tiles = new List<(int, int, short)>(count);

        for (var i = 0; i < count; i++)
            tiles.Add((reader.Byte(), reader.Byte(), reader.Short()));

        return tiles;
    }

    public static FloorPlan FloorHeightMap(PacketReader reader)
    {
        var small = reader.Bool();
        var fixedWallsHeight = reader.Int();
        var model = reader.String();

        return new FloorPlan(small, fixedWallsHeight, model);
    }

    public static (int RoomId, bool IsOwner) RoomEntryInfo(PacketReader reader) =>
        (reader.Int(), reader.Bool());

    /// <summary>Chat, shout and whisper all share <c>ChatMessagePayloadSerializer</c>.</summary>
    public static ChatLine Chat(PacketReader reader)
    {
        var objectId = reader.Int();
        var text = reader.String();
        _ = reader.Int(); // gesture
        var styleId = reader.Int();

        return new ChatLine(objectId, text, styleId);
    }

    /// <summary>
    /// The model text as rows, whichever line break the sender used, without trailing empties.
    /// </summary>
    public static string[] ModelRows(string modelData) =>
        modelData
            .Replace("\r\n", "\r", StringComparison.Ordinal)
            .Replace('\n', '\r')
            .Split('\r', StringSplitOptions.RemoveEmptyEntries);

    private static void SkipOwnerNames(PacketReader reader)
    {
        var owners = reader.Count();

        for (var i = 0; i < owners; i++)
        {
            _ = reader.Int();
            _ = reader.String();
        }
    }
}
