using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Quest;

public record RejectQuestMessage : IMessageEvent
{
    public required int QuestId { get; init; }
}
