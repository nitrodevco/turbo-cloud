using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A bot standing in a room, as staff set it.</summary>
public sealed record RoomBotRequest(
    string? Name,
    string? Motto,
    string? Figure,
    AvatarGenderType? Gender,
    string? ChatText,
    bool? AutoChat,
    int? ChatDelaySeconds,
    bool? MixSentences,
    bool? FreeRoam,
    AvatarDanceType? Dance
);
