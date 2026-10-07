using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>The user accepts a wired trade; the second, final confirm completes it.</summary>
public record WiredTradeConfirmMessage : IMessageEvent
{
    public required bool IsFinalConfirm { get; init; }
}
