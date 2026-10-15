using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Quest;

/// <summary>The reception's daily quest: the <see cref="Index"/>th quest of one difficulty.</summary>
public record GetDailyQuestMessage : IMessageEvent
{
    /// <summary>Whether an easy quest is asked for, else a hard one.</summary>
    public required bool Easy { get; init; }

    /// <summary>0 on a refresh, one more for each "another quest".</summary>
    public required int Index { get; init; }
}
