using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Poll;

/// <summary>The player starts an offered poll (Flash <c>PollStartComposer</c>, the poll id).</summary>
public record PollStartMessage : IMessageEvent
{
    public required int PollId { get; init; }
}
