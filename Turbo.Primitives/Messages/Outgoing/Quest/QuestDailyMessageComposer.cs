using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>
/// The reception's daily quest, or none when every quest is done; with one, how many quests of
/// each difficulty there are to choose from.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record QuestDailyMessageComposer : IComposer
{
    [Id(0)]
    public required QuestSnapshot? Quest { get; init; }

    [Id(1)]
    public required int EasyQuestCount { get; init; }

    [Id(2)]
    public required int HardQuestCount { get; init; }
}
