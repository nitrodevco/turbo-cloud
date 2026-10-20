using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>
/// "Synchronize" on a variable share add-on (Flash <c>SynchronizeSharedAddonsMessageComposer(wiredId)</c>,
/// sent by <c>VariableAddonShareAddon.synchronize</c>, WIN63-202610201032).
/// </summary>
public record SynchronizeSharedAddonsMessage : IMessageEvent
{
    /// <summary>The share add-on box being edited.</summary>
    public required int WiredId { get; init; }
}
