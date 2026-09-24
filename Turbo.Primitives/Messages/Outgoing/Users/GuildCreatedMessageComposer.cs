using Orleans;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// A group was made. The client shows its welcome window and, if the creator is not standing in
/// the new homeroom, walks them to it — which is why the room id is here and not just the group.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildCreatedMessageComposer : IComposer
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required GuildId GuildId { get; init; }
}
