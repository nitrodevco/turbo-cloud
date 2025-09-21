using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins;

internal static class PluginDependencyResolver
{
    public static IReadOnlyList<PluginManifest> SortManifests(
        IReadOnlyList<PluginManifest> manifests
    )
    {
        var byKey = manifests.ToDictionary(m => m.Key, StringComparer.OrdinalIgnoreCase);
        var indeg = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var graph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in manifests)
        {
            indeg[m.Key] = 0;
            graph[m.Key] = new();
        }
        foreach (var m in manifests)
        {
            foreach (var d in m.Dependencies)
            {
                if (!byKey.ContainsKey(d.Key))
                    throw new InvalidOperationException($"{m.Key} depends on missing {d.Key}");
                graph[d.Key].Add(m.Key);
                indeg[m.Key]++;
            }
        }

        var q = new Queue<string>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var order = new List<string>();
        while (q.Count > 0)
        {
            var k = q.Dequeue();
            order.Add(k);
            foreach (var n in graph[k])
                if (--indeg[n] == 0)
                    q.Enqueue(n);
        }
        if (order.Count != manifests.Count)
            throw new InvalidOperationException("Cyclic plugin dependencies.");
        return order.Select(k => byKey[k]).ToList();
    }
}
