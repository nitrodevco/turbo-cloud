using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>
/// Tasks added to the player's list. When the first is a bonus task the client says
/// <c>dailytasks.bonus_available</c>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record DailyTasksTasksAddedMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<DailyTaskSnapshot> Tasks { get; init; }
}
