using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Packets;

namespace Turbo.Networking.Extensions;

public sealed class ExtensionPacketRegistry : IExtensionPacketRegistry
{
    private readonly ConcurrentDictionary<int, IParser> _parsers = new();
    private readonly ConcurrentDictionary<Type, ISerializer> _serializers = new();
    private readonly ConcurrentDictionary<string, object> _capabilities = new(
        StringComparer.Ordinal
    );

    public IDisposable RegisterParser(int header, IParser parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        RequirePluginHeader(header);

        return Add(_parsers, header, parser, $"Header {header} already has a parser.");
    }

    public IDisposable RegisterSerializer(Type composerType, ISerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(composerType);
        ArgumentNullException.ThrowIfNull(serializer);
        RequirePluginHeader(serializer.Header);

        return Add(
            _serializers,
            composerType,
            serializer,
            $"{composerType.Name} already has a serializer."
        );
    }

    public IDisposable RegisterCapability(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (ClientCapabilities.SUPPORTED.ContainsKey(name))
            throw new ArgumentException($"\"{name}\" is a core capability.", nameof(name));

        return Add(_capabilities, name, new object(), $"Capability \"{name}\" already exists.");
    }

    public bool TryGetParser(int header, out IParser parser) =>
        _parsers.TryGetValue(header, out parser!);

    public bool TryGetSerializer(Type composerType, out ISerializer serializer) =>
        _serializers.TryGetValue(composerType, out serializer!);

    public bool HasCapability(string name) => _capabilities.ContainsKey(name);

    private static void RequirePluginHeader(int header)
    {
        if (!ExtensionPackets.IsPluginHeader(header))
            throw new ArgumentOutOfRangeException(
                nameof(header),
                header,
                "Not a plugin header (20000-32767, excluding 30000-30099)."
            );
    }

    private static Registration Add<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> map,
        TKey key,
        TValue value,
        string duplicateMessage
    )
        where TKey : notnull
    {
        if (!map.TryAdd(key, value))
            throw new InvalidOperationException(duplicateMessage);

        // Disposing removes the entry only while it is still the one added here.
        return new Registration(() => map.TryRemove(new KeyValuePair<TKey, TValue>(key, value)));
    }

    private sealed class Registration(Func<bool> remove) : IDisposable
    {
        public void Dispose() => remove();
    }
}
