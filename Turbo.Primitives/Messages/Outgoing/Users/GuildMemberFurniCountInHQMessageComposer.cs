using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// How much furni this member has standing in the homeroom. The client asks before it kicks or
/// leaves so its confirmation can say how much is at stake, and sends the kick only once the
/// answer is back.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildMemberFurniCountInHQMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required int FurniCount { get; init; }
}
