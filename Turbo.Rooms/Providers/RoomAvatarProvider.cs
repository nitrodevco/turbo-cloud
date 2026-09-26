using Microsoft.Extensions.Options;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Avatars.Bot;
using Turbo.Rooms.Object.Avatars.Pet;
using Turbo.Rooms.Object.Avatars.Player;

namespace Turbo.Rooms.Providers;

public sealed class RoomAvatarProvider(IOptions<BotConfig> botConfig) : IRoomAvatarProvider
{
    private readonly BotConfig _botConfig = botConfig.Value;

    public IRoomPlayer CreateAvatarFromPlayerSnapshot(
        RoomObjectId objectId,
        PlayerSummarySnapshot snapshot
    )
    {
        var avatar = new RoomPlayerAvatar { ObjectId = objectId, PlayerId = snapshot.PlayerId };

        avatar.UpdateWithPlayer(snapshot);

        return avatar;
    }

    public IRoomPet CreateAvatarFromPetSnapshot(RoomObjectId objectId, PetSnapshot snapshot) =>
        RoomPetAvatar.FromSnapshot(objectId, snapshot);

    public IRoomBot CreateAvatarFromBotSnapshot(RoomObjectId objectId, BotSnapshot snapshot) =>
        RoomBotAvatar.FromSnapshot(objectId, snapshot, [.. _botConfig.Skills]);
}
