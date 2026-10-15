using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Quest;

/// <summary>
/// The hotel view's daily quest asks for a quest: of the easy or hard ones, the
/// <see cref="Index"/>-th ("next quest" counts it up), <c>GetDailyQuestMessageComposer(easy, index)</c>.
/// </summary>
public record GetDailyQuestMessage : IMessageEvent
{
    public required bool Easy { get; init; }

    public required int Index { get; init; }
}
