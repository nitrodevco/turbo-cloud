using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record GuildFurniContextMenuInfoMessageComposer : IComposer
{
    [Id(0)]
    public required int ObjectId { get; init; }

    [Id(1)]
    public required int GuildId { get; init; }

    [Id(2)]
    public required string GuildName { get; init; }

    [Id(3)]
    public required int GuildHomeRoomId { get; init; }

    [Id(4)]
    public required bool UserIsMember { get; init; }

    [Id(5)]
    public required bool GuildHasReadableForum { get; init; }
}
