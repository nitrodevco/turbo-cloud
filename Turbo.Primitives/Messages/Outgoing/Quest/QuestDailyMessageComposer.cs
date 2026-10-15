using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>
/// The hotel view's daily quest (<c>QuestDailyMessageParser</c>): the quest offered, or none
/// (the "all done" text), and how many easy and hard quests there are to go through.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record QuestDailyMessageComposer : IComposer
{
    [Id(0)]
    public required QuestSnapshot? Quest { get; init; }

    [Id(1)]
    public int EasyQuestCount { get; init; }

    [Id(2)]
    public int HardQuestCount { get; init; }
}
