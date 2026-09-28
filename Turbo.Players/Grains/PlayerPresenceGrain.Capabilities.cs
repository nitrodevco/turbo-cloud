using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public Task SetClientCapabilitiesAsync(
        ImmutableArray<ClientCapabilitySnapshot> capabilities,
        CancellationToken ct
    )
    {
        // Only a session can have asked; one arriving with none attached is dropped.
        if (_sessionObserver is null)
            return Task.CompletedTask;

        _state.ClientCapabilities = capabilities.ToImmutableDictionary(
            x => x.Name,
            x => x.Version,
            StringComparer.Ordinal
        );

        return Task.CompletedTask;
    }

    /// <summary>Whether the current session accepted the extension <paramref name="composer"/> belongs to.</summary>
    private bool AcceptsExtension(ICapabilityComposer composer) =>
        _state.ClientCapabilities.ContainsKey(composer.Capability);
}
