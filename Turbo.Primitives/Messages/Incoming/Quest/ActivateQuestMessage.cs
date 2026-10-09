using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Quest;

public record ActivateQuestMessage : IMessageEvent
{
    public required int QuestId { get; init; }
}
