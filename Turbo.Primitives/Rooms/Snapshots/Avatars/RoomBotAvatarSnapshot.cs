using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Avatars;

[GenerateSerializer, Immutable]
public sealed record RoomBotAvatarSnapshot : RoomAvatarSnapshot { }
