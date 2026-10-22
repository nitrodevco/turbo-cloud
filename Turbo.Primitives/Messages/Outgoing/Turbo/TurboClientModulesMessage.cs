using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Messages.Outgoing.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>client.modules</c>): every client module the hotel's plugins
/// bring, the whole set, sent after the capability answer and again whenever it changes. A client
/// loads the ones it has not loaded, and takes away the ones no longer listed.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TurboClientModulesMessage : ICapabilityComposer
{
    [Id(0)]
    public required ImmutableArray<ClientModuleSnapshot> Modules { get; init; }

    public string Capability => ClientCapabilities.CLIENT_MODULES;
}
