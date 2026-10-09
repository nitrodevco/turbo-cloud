using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>Every daily task the player holds; the client replaces its list with it.</summary>
[GenerateSerializer, Immutable]
public sealed record DailyTasksActiveListMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<DailyTaskSnapshot> Tasks { get; init; }
}
