using Orleans;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Groupforums;

/// <summary>A message as it is now (hidden or restored).</summary>
[GenerateSerializer, Immutable]
public sealed record UpdateMessageMessageComposer : IComposer
{
    [Id(0)]
    public required int GroupId { get; init; }

    [Id(1)]
    public required int ThreadId { get; init; }

    [Id(2)]
    public required GuildForumMessageSnapshot Message { get; init; }
}
