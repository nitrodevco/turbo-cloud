using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>The quests a player can see, in answer to <c>GetQuests</c>.</summary>
[GenerateSerializer, Immutable]
public sealed record QuestsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<QuestSnapshot> Quests { get; init; }

    /// <summary>Whether the client should open its quest window on this list.</summary>
    [Id(1)]
    public required bool OpenWindow { get; init; }
}
