using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Furni;
using Turbo.Contracts.Plugins;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Testing;

/// <summary>A plugin live in a <see cref="TestHotel"/>: its manifest, its services, its tables.</summary>
public sealed class TestPlugin
{
    private readonly IReadOnlyDictionary<string, string> _texts;

    internal TestPlugin(
        PluginManifest manifest,
        Assembly assembly,
        IServiceProvider services,
        string folder
    )
    {
        Manifest = manifest;
        Assembly = assembly;
        Services = services;
        _texts = ReadTexts(Path.Combine(folder, "gamedata", "external_texts.txt"));
    }

    public PluginManifest Manifest { get; }

    public string Key => Manifest.Key;

    /// <summary>The test's build of the plugin, the one running.</summary>
    public Assembly Assembly { get; }

    /// <summary>The plugin's own services, as its classes are given them.</summary>
    public IServiceProvider Services { get; }

    /// <summary>One of the plugin's own services (its singleton, or a new one of a scoped service).</summary>
    public T Service<T>()
        where T : notnull => Services.GetRequiredService<T>();

    /// <summary>The plugin's settings now, as its classes read them.</summary>
    public T Settings<T>()
        where T : class => Services.GetRequiredService<IOptionsMonitor<T>>().CurrentValue;

    /// <summary>
    /// A new one of the plugin's database contexts, over the hotel's in-memory database: what its
    /// code saved is there to read, and rows written here are there for its code. Dispose it.
    /// </summary>
    public T Db<T>()
        where T : DbContext => Services.CreateScope().ServiceProvider.GetRequiredService<T>();

    /// <summary>A new one of the plugin's classes, built as the server builds its classes: its services, then the host's.</summary>
    public T Create<T>()
        where T : class =>
        ActivatorUtilities.CreateInstance<T>(
            new CompositeServiceProvider(
                Services,
                Services.GetRequiredService<IHostServices>() as IServiceProvider ?? Services
            )
        );

    /// <summary>The plugin's text of <paramref name="key"/> (its <c>gamedata/external_texts.txt</c>); null when it has none.</summary>
    public string? Text(string key) => _texts.GetValueOrDefault(key);

    internal IEnumerable<string> TextKeys => _texts.Keys;

    internal bool HasFurniLogic(string logic) =>
        AssemblyExplorer
            .FindAssignees(Assembly, typeof(FurniLogic))
            .Any(t =>
                string.Equals(
                    t.GetCustomAttribute<FurniLogicAttribute>()?.Key,
                    logic,
                    StringComparison.OrdinalIgnoreCase
                )
            );

    private static Dictionary<string, string> ReadTexts(string file)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!File.Exists(file))
            return texts;

        foreach (var line in File.ReadLines(file))
        {
            var at = line.IndexOf('=');

            if (at > 0 && !line.StartsWith('#'))
                texts[line[..at].Trim()] = line[(at + 1)..];
        }

        return texts;
    }

    /// <summary>The plugin's furni logic class for <paramref name="logic"/> (its <c>[FurniLogic]</c> key), built as the room builds it.</summary>
    internal FurniLogic LogicFor(string logic)
    {
        var type =
            AssemblyExplorer
                .FindAssignees(Assembly, typeof(FurniLogic))
                .FirstOrDefault(t =>
                    string.Equals(
                        t.GetCustomAttribute<FurniLogicAttribute>()?.Key,
                        logic,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            ?? throw new HotelAssertionException(
                $"Plugin {Key} has no [FurniLogic(\"{logic}\")] class."
            );

        return (FurniLogic)
            ActivatorUtilities.CreateInstance(
                new CompositeServiceProvider(
                    Services,
                    Services.GetRequiredService<IHostServices>() as IServiceProvider ?? Services
                ),
                type
            );
    }

    public override string ToString() => $"{Manifest.Name} ({Key})";
}
