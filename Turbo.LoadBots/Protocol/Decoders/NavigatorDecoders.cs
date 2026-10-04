using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.LoadBots.Protocol.Decoders;

/// <summary>A room as the navigator lists it.</summary>
public sealed record RoomListing(
    int RoomId,
    string Name,
    int OwnerId,
    string OwnerName,
    int DoorMode,
    int Users,
    int MaxUsers
);

/// <summary>
/// Decoders for the navigator messages, mirroring
/// <c>Turbo.Revisions/Revision20260909/Serializers/NewNavigator</c> and
/// <c>Serializers/Navigator</c>.
/// </summary>
public static class NavigatorDecoders
{
    public static (int RoomId, string Name) FlatCreated(PacketReader reader) =>
        (reader.Int(), reader.String());

    /// <summary>Every room in every block of a search result.</summary>
    public static List<RoomListing> SearchResultBlocks(PacketReader reader)
    {
        _ = reader.String(); // search code
        _ = reader.String(); // filter

        var rooms = new List<RoomListing>();
        var blocks = reader.Count();

        for (var b = 0; b < blocks; b++)
        {
            _ = reader.String(); // block code
            _ = reader.String(); // text
            _ = reader.Int(); // action allowed
            _ = reader.Bool(); // force closed
            _ = reader.Int(); // view mode

            var count = reader.Count();

            for (var i = 0; i < count; i++)
                rooms.Add(Room(reader));
        }

        return rooms;
    }

    public static RoomListing Room(PacketReader reader)
    {
        var roomId = reader.Int();
        var name = reader.String();
        var ownerId = reader.Int();
        var ownerName = reader.String();
        var doorMode = reader.Int();
        var users = reader.Int();
        var maxUsers = reader.Int();
        _ = reader.String(); // description
        _ = reader.Int(); // trade type
        _ = reader.Int(); // score
        _ = reader.Int(); // ranking
        _ = reader.Int(); // category
        _ = reader.Strings(); // tags

        var bitmask = (RoomBitmaskFlags)reader.Int();

        if (bitmask.HasFlag(RoomBitmaskFlags.Thumbnail))
            _ = reader.String();

        if (bitmask.HasFlag(RoomBitmaskFlags.GroupData))
        {
            _ = reader.Int();
            _ = reader.String();
            _ = reader.String();
        }

        if (bitmask.HasFlag(RoomBitmaskFlags.RoomAd))
        {
            _ = reader.String();
            _ = reader.String();
            _ = reader.Int();
        }

        return new RoomListing(roomId, name, ownerId, ownerName, doorMode, users, maxUsers);
    }
}
