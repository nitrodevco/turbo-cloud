using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>
/// The seasonal quests, in answer to <c>GetSeasonalQuestsOnly</c>: the <see cref="QuestsMessageComposer"/>
/// list kept to its seasonal quests, with no "open window" flag (<c>SeasonalQuestsMessageParser</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SeasonalQuestsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<QuestSnapshot> Quests { get; init; }
}
