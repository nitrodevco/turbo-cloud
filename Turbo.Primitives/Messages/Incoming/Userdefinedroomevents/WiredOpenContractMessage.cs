using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>The client answers a WiredOpenContractMessageComposer by asking for that contract's contents.</summary>
public record WiredOpenContractMessage : IMessageEvent
{
    public required int ContractId { get; init; }
}
