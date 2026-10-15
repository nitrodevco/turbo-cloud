using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Poll;

/// <summary>The player turns an offered poll down (Flash <c>PollRejectComposer</c>, the poll id).</summary>
public record PollRejectMessage : IMessageEvent
{
    public required int PollId { get; init; }
}
