using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots;

/// <summary>
/// The live navigator view of an active room, as the room grain last published it. It carries
/// room info only (no password or moderation settings), so it is safe to hand to any caller.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomActiveSnapshot : RoomInfoSnapshot
{
    public static RoomActiveSnapshot From(RoomInfoSnapshot room, int population) =>
        new()
        {
            RoomId = room.RoomId,
            Name = room.Name,
            Description = room.Description,
            OwnerId = room.OwnerId,
            OwnerName = room.OwnerName,
            Population = population,
            DoorMode = room.DoorMode,
            PlayersMax = room.PlayersMax,
            TradeType = room.TradeType,
            Score = room.Score,
            Ranking = room.Ranking,
            CategoryId = room.CategoryId,
            Tags = room.Tags,
            AllowBlocking = room.AllowBlocking,
            AllowPets = room.AllowPets,
            AllowPetsEat = room.AllowPetsEat,
            StaffPick = room.StaffPick,
            ActiveEvent = room.ActiveEvent,
            LastUpdatedUtc = room.LastUpdatedUtc,
        };
}
