using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userclassification;

/// <summary><c>:uc hotel &lt;class&gt;</c>: the players online of a class, every class when empty.</summary>
public record PeerUsersClassificationMessage : IMessageEvent
{
    public required string Classification { get; init; }
}
