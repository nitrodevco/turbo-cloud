using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>The quest the player was doing, dropped (or run out).</summary>
[GenerateSerializer, Immutable]
public sealed record QuestCancelledMessageComposer : IComposer
{
    /// <summary>Whether it ran out rather than being dropped.</summary>
    [Id(0)]
    public required bool Expired { get; init; }

    [Id(1)]
    public required QuestSnapshot Quest { get; init; }
}
