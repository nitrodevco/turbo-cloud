using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Plugins.Messages.Snapshots;
using Turbo.Runtime;

namespace Turbo.Messages.PluginMessages;

/// <summary>
/// The plugins' named messages: their headers, given from the top of the plugin header space down
/// (hand-numbered plugin packets start at its bottom) past every header a packet already uses, and
/// the parser each one's header reads with. A name keeps its header for the life of the process,
/// so a reloaded plugin's messages come back where they were while nothing took the header since.
/// </summary>
internal sealed class PluginMessageDirectory(IExtensionPacketRegistry extensions)
    : IPluginMessageDirectory
{
    private readonly IExtensionPacketRegistry _extensions = extensions;
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _live = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _given = new(StringComparer.Ordinal);
    private ImmutableArray<PluginMessageSnapshot> _messages = [];

    public ImmutableArray<PluginMessageSnapshot> Messages => _messages;

    public event Action? Changed;

    public bool TryGetHeader(string name, out int header)
    {
        lock (_gate)
        {
            header = _live.TryGetValue(name, out var entry) ? entry.Header : 0;

            return header != 0;
        }
    }

    public IDisposable Register(string owner, IReadOnlyCollection<Type> messageTypes)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);
        ArgumentNullException.ThrowIfNull(messageTypes);

        var schemas = messageTypes.Select(PluginMessageSchema.For).ToList();

        foreach (var schema in schemas)
        {
            if (!schema.Name.StartsWith(owner + ".", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"Plugin message \"{schema.Name}\" ({schema.Type.Name}) must start with the plugin's key and a dot: \"{owner}.\".",
                    nameof(messageTypes)
                );
        }

        var added = new List<Entry>();
        var parsers = new CompositeDisposable();

        lock (_gate)
        {
            try
            {
                foreach (var schema in schemas)
                {
                    if (_live.TryGetValue(schema.Name, out var other))
                        throw new InvalidOperationException(
                            $"Plugin message \"{schema.Name}\" is already registered by {other.Owner}."
                        );

                    var header = NextHeader(schema.Name);

                    parsers.Add(
                        _extensions.RegisterParser(header, new PluginMessageParser(schema))
                    );

                    var entry = new Entry(owner, schema, header);

                    _live[schema.Name] = entry;
                    _given[schema.Name] = header;
                    added.Add(entry);
                }
            }
            catch
            {
                foreach (var entry in added)
                    _live.Remove(entry.Schema.Name);

                parsers.Dispose();

                throw;
            }

            Rebuild();
        }

        if (added.Count > 0)
            Changed?.Invoke();

        return new ActionDisposable(() =>
        {
            lock (_gate)
            {
                foreach (var entry in added)
                {
                    if (_live.TryGetValue(entry.Schema.Name, out var live) && live == entry)
                        _live.Remove(entry.Schema.Name);
                }

                parsers.Dispose();
                Rebuild();
            }

            if (added.Count > 0)
                Changed?.Invoke();
        });
    }

    // The header the name had before when it is still free, else the highest free one that no
    // other name had (so an unloaded plugin's names find theirs again), else the highest free one.
    private int NextHeader(string name)
    {
        if (_given.TryGetValue(name, out var before) && IsFree(before))
            return before;

        var givenBefore = _given.Values.ToHashSet();

        return FirstFree(x => !givenBefore.Contains(x))
            ?? FirstFree(_ => true)
            ?? throw new InvalidOperationException(
                "No plugin header is free for another plugin message."
            );
    }

    private int? FirstFree(Func<int, bool> allowed)
    {
        for (
            var header = ExtensionPackets.LAST_PLUGIN_HEADER;
            header >= ExtensionPackets.FIRST_PLUGIN_HEADER;
            header--
        )
        {
            if (ExtensionPackets.IsPluginHeader(header) && allowed(header) && IsFree(header))
                return header;
        }

        return null;
    }

    private bool IsFree(int header) =>
        !_extensions.HasHeader(header) && _live.Values.All(x => x.Header != header);

    private void Rebuild() =>
        _messages = [
            .. _live
                .Values.OrderBy(x => x.Schema.Name, StringComparer.Ordinal)
                .Select(x => new PluginMessageSnapshot
                {
                    Name = x.Schema.Name,
                    Header = x.Header,
                    Schema = x.Schema.Json,
                }),
        ];

    private sealed record Entry(string Owner, PluginMessageSchema Schema, int Header);
}
