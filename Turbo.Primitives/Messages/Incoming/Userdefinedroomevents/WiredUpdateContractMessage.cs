using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>The user saves a wired contract; answered by WiredContractUpdateResultMessageComposer.</summary>
public record WiredUpdateContractMessage : IMessageEvent
{
    public required WiredContractSnapshot Contract { get; init; }
}
