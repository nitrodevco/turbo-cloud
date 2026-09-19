using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Primitives.Rooms.Providers;

public interface IRoomAvatarProvider
{
    public IRoomPlayer CreateAvatarFromPlayerSnapshot(
        RoomObjectId objectId,
        PlayerSummarySnapshot snapshot
    );
    public IRoomPet CreateAvatarFromPetSnapshot(RoomObjectId objectId, PetSnapshot snapshot);
    public IRoomBot CreateAvatarFromBotSnapshot(RoomObjectId objectId, BotSnapshot snapshot);
}
