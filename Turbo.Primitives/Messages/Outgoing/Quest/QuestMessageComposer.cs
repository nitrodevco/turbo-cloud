using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>One quest, accepted or with new progress.</summary>
[GenerateSerializer, Immutable]
public sealed record QuestMessageComposer : IComposer
{
    [Id(0)]
    public required QuestSnapshot Quest { get; init; }
}
