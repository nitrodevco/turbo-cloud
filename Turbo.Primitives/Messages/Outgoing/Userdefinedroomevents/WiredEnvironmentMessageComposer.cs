using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// Whether the room has a "user clicks user" trigger. While it does, the client reports clicks on
/// avatars with <c>WiredClickUser</c>, holds the avatar menu until the room answers, and stops
/// turning its own avatar towards whoever it clicked (Flash <c>WiredEnvironment</c>,
/// <c>RoomObjectEventHandler.setSelectedAvatar</c>). After the flag come the achievements the
/// room's Achievement Enabler add-ons enable, read only when there are bytes left: the
/// <c>ProgressAchievement</c> editor lists them (<c>HabboUserDefinedRoomEvents.achievementsInRoom</c>)
/// and the achievements window's "wired_games" category shows the <c>WF_</c> ones named here
/// (<c>AchievementController.achievementIsVisible</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredEnvironmentMessageComposer : IComposer
{
    [Id(0)]
    public required bool HasClickUserWired { get; init; }

    /// <summary>The enabled achievement names, without their <c>ACH_WF_</c> prefix.</summary>
    [Id(1)]
    public ImmutableArray<string> EnabledAchievements { get; init; } = [];
}
