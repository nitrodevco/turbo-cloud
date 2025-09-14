using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Pipeline;

internal class CompositeEnvelopeScope : IAsyncDisposable
{
    public const string HostOwnerId = "host";

    private readonly Dictionary<string, OwnerCtx> _owners;

    private CompositeEnvelopeScope(Dictionary<string, OwnerCtx> owners) => _owners = owners;

    public static CompositeEnvelopeScope Begin(
        IServiceProvider hostRoot,
        bool createHostAmbientScope,
        IReadOnlyDictionary<string, IServiceProvider> pluginRoots
    )
    {
        var owners = new Dictionary<string, OwnerCtx>(1 + pluginRoots.Count, StringComparer.Ordinal)
        {
            // Host slot
            [HostOwnerId] = OwnerCtx.Create(hostRoot, createAmbient: createHostAmbientScope),
        };

        foreach (var (ownerId, root) in pluginRoots)
            owners[ownerId] = OwnerCtx.Create(root, createAmbient: true);

        return new CompositeEnvelopeScope(owners);
    }

    public IServiceProvider AmbientForOwner(string ownerId)
    {
        // Exact owner match
        if (_owners.TryGetValue(ownerId, out var ctx))
            return ctx.AmbientProvider; // never null

        // Fallback to host (should always exist)
        if (_owners.TryGetValue(HostOwnerId, out var host))
            return host.AmbientProvider;

        // If we get here, the composite was constructed without a host slot.
        throw new InvalidOperationException(
            "CompositeEnvelopeScope is missing the host owner. Ensure Begin(...) adds HostOwnerId."
        );
    }

    public IServiceProvider RootForOwner(string ownerId) =>
        _owners.TryGetValue(ownerId, out var ctx) ? ctx.Root : _owners[HostOwnerId].Root;

    public AsyncServiceScope CreateChildScopeFor(string ownerId) =>
        _owners.TryGetValue(ownerId, out var ctx)
            ? ctx.ScopeFactory.CreateAsyncScope()
            : _owners[HostOwnerId].ScopeFactory.CreateAsyncScope();

    public bool TryGetAmbient(string ownerId, out IServiceProvider sp)
    {
        if (_owners.TryGetValue(ownerId, out var ctx))
        {
            if (ctx.AmbientProvider is IServiceProvider serviceProvider)
            {
                sp = serviceProvider;

                return true;
            }
        }

        sp = default!;

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        // Dispose any ambient scopes we created
        foreach (var kv in _owners.Values)
            await kv.DisposeAsync().ConfigureAwait(false);
    }
}
