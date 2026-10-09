using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>
/// A task's progress or state changed. The client shows <c>dailytasks.completed.caption</c>
/// when it turns completed and <c>dailytasks.claimed.caption</c> when it turns claimed.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record DailyTasksTaskUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required long TaskId { get; init; }

    [Id(1)]
    public required int Repeats { get; init; }

    [Id(2)]
    public required DailyTaskStatus Status { get; init; }

    [Id(3)]
    public required int SecondsLeft { get; init; }
}
