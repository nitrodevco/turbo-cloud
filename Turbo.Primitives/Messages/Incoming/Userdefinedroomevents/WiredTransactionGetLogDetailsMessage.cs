using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>The user asks for the details of one wired transaction.</summary>
public record WiredTransactionGetLogDetailsMessage : IMessageEvent
{
    public required long TransactionId { get; init; }
}
