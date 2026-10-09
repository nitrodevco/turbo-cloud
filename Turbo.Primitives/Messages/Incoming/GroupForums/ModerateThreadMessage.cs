using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Groupforums;

public record ModerateThreadMessage : IMessageEvent
{
    public required int GroupId { get; init; }

    public required int ThreadId { get; init; }

    public required int State { get; init; }
}
