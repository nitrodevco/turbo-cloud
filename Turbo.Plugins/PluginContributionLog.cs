using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins;

/// <summary>
/// What one load of a plugin registered (<see cref="IPluginContributionLog"/>), in its container
/// from before anything is registered; kept for the inspector after the load, failed or not. A
/// thing noted twice is listed once.
/// </summary>
internal sealed class PluginContributionLog : IPluginContributionLog
{
    private readonly List<PluginContribution> _items = [];

    public void Add(string kind, string name, string? detail = null)
    {
        var item = new PluginContribution(kind, name, detail);

        lock (_items)
            if (!_items.Contains(item))
                _items.Add(item);
    }

    public ImmutableArray<PluginContribution> Items
    {
        get
        {
            lock (_items)
                return
                [
                    .. _items
                        .OrderBy(x => x.Kind, StringComparer.Ordinal)
                        .ThenBy(x => x.Name, StringComparer.Ordinal),
                ];
        }
    }
}
