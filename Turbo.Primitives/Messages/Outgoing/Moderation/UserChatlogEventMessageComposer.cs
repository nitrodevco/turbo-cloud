using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

/// <summary>A player's chatlog: one record per room, newest room first.</summary>
[GenerateSerializer, Immutable]
public sealed record UserChatlogEventMessageComposer : IComposer
{
    [Id(0)]
    public required int UserId { get; init; }

    [Id(1)]
    public required string UserName { get; init; }

    [Id(2)]
    public required ImmutableArray<ModChatlogRecordSnapshot> Records { get; init; }
}
