using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Plugins.Messages.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>client.messages</c>): every plugin message the hotel has, by
/// name, with the header the host gave it and its field schema; the whole set, sent after the
/// capability answer and again whenever it changes. A client reads and writes plugin messages by
/// name from it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TurboPluginMessagesMessage : ICapabilityComposer
{
    [Id(0)]
    public required ImmutableArray<PluginMessageSnapshot> Messages { get; init; }

    public string Capability => ClientCapabilities.CLIENT_MESSAGES;
}
