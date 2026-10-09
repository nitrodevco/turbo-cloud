using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>A quest done; the client shows its completion dialog when asked.</summary>
[GenerateSerializer, Immutable]
public sealed record QuestCompletedMessageComposer : IComposer
{
    [Id(0)]
    public required QuestSnapshot Quest { get; init; }

    [Id(1)]
    public required bool ShowDialog { get; init; }
}
