using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userclassification;

/// <summary>
/// <c>:uc &lt;class&gt;</c> or <c>:anew</c> (class <c>new</c>): the players in the sender's room of a
/// class, every class when empty.
/// </summary>
public record RoomUsersClassificationMessage : IMessageEvent
{
    public required string Classification { get; init; }
}
