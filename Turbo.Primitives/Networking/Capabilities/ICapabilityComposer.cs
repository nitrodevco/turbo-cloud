namespace Turbo.Primitives.Networking.Capabilities;

/// <summary>
/// A composer that belongs to one of Turbo's protocol extensions rather than to Habbo's protocol.
/// The player's presence sends it only to a session that accepted <see cref="Capability"/>, so the
/// grain composing it never has to ask; any other client never receives it.
/// </summary>
public interface ICapabilityComposer : IComposer
{
    /// <summary>The extension the composer belongs to: one of <see cref="ClientCapabilities"/>.</summary>
    public string Capability { get; }
}
