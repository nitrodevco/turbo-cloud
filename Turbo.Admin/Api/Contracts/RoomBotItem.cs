using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A bot standing in a room: whose it is and how it is set.</summary>
public sealed record RoomBotItem(
    int Id,
    string Name,
    string Motto,
    string Figure,
    AvatarGenderType Gender,
    int OwnerId,
    string OwnerName,
    string ChatText,
    bool AutoChat,
    int ChatDelaySeconds,
    bool MixSentences,
    bool FreeRoam,
    AvatarDanceType Dance
);
