using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Packets;

namespace Turbo.Plugins;

/// <summary>
/// What one plugin load registered in the protocol (<see cref="IExtensionPacketRegistry"/>), so
/// the admin panel can say which plugin owns a capability or header. Registrations come and go
/// with the plugin's own handles: disposing one forgets it here too.
/// </summary>
internal sealed class PluginExtensions
{
    private readonly ConcurrentDictionary<string, byte> _capabilities = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<int, byte> _incoming = new();
    private readonly ConcurrentDictionary<int, byte> _outgoing = new();

    public ImmutableArray<string> Capabilities =>
        [.. _capabilities.Keys.Order(StringComparer.Ordinal)];

    public ImmutableArray<int> IncomingHeaders => [.. _incoming.Keys.Order()];

    public ImmutableArray<int> OutgoingHeaders => [.. _outgoing.Keys.Order()];

    /// <summary>The registry as this plugin sees it: the host's, noting what it adds.</summary>
    public IExtensionPacketRegistry Wrap(IExtensionPacketRegistry inner) =>
        new RecordingRegistry(inner, this);

    private static IDisposable Track<TKey>(
        ConcurrentDictionary<TKey, byte> set,
        TKey key,
        IDisposable registration
    )
        where TKey : notnull
    {
        set[key] = 0;

        return new Registration(registration, () => set.TryRemove(key, out _));
    }

    private sealed class Registration(IDisposable inner, Action forget) : IDisposable
    {
        public void Dispose()
        {
            inner.Dispose();
            forget();
        }
    }

    private sealed class RecordingRegistry(IExtensionPacketRegistry inner, PluginExtensions owner)
        : IExtensionPacketRegistry
    {
        public IDisposable RegisterParser(int header, IParser parser) =>
            Track(owner._incoming, header, inner.RegisterParser(header, parser));

        public IDisposable RegisterSerializer(Type composerType, ISerializer serializer) =>
            Track(
                owner._outgoing,
                serializer.Header,
                inner.RegisterSerializer(composerType, serializer)
            );

        public IDisposable RegisterCapability(string name) =>
            Track(owner._capabilities, name, inner.RegisterCapability(name));

        public bool TryGetParser(int header, out IParser parser) =>
            inner.TryGetParser(header, out parser);

        public bool TryGetSerializer(Type composerType, out ISerializer serializer) =>
            inner.TryGetSerializer(composerType, out serializer);

        public bool HasCapability(string name) => inner.HasCapability(name);

        public bool HasHeader(int header) => inner.HasHeader(header);
    }
}
