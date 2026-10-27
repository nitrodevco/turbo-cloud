using System;
using System.Collections.Generic;
using System.IO;
using Turbo.Contracts.Plugins;
using Turbo.Plugins.ClientBuild;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins;

/// <summary>
/// The client module registry as one plugin reaches it (<see cref="IClientModuleRegistry"/>): its
/// module is built by the server from its folder's <c>client/src/</c> (<see cref="PluginClientBuilder"/>)
/// and served as <c>&lt;key&gt;.js</c> beside its <c>wwwroot/</c>, at its manifest's version. What it
/// registered and did not take away itself is taken away when it unloads. Its <c>wwwroot/</c> is
/// served from the load (<see cref="ServeFiles"/>), module or not, for the files its own variables
/// name (a theme's template libraries), until it unloads.
/// </summary>
internal sealed class PluginClientModules(
    IClientModuleDirectory directory,
    PluginManifest manifest,
    string folder,
    PluginClientBuilder builder,
    PluginContributionLog? contributions = null
) : IClientModuleRegistry, IDisposable
{
    /// <summary>The folder under a plugin's own that its client files are served from.</summary>
    public const string WWWROOT = "wwwroot";

    private readonly List<IDisposable> _registrations = [];
    private bool _registered;
    private (string Entry, PluginClientBuild Build)? _conventional;

    /// <summary>Serves its <c>wwwroot/</c>, when it has one, until it unloads.</summary>
    public void ServeFiles()
    {
        var root = Path.Combine(folder, WWWROOT);

        if (!Directory.Exists(root))
            return;

        var registration = directory.AddFiles(manifest.Key, root);

        lock (_registrations)
            _registrations.Add(registration);
    }

    /// <summary>
    /// Builds its module from <c>client/src/index.tsx</c> (or <c>index.ts</c>) when it has one,
    /// before the plugin starts: a module that does not build fails the load. Answers the build, or
    /// null for a plugin with no client sources.
    /// </summary>
    public PluginClientBuild? BuildConventionalModule()
    {
        if (PluginClientBuilder.ConventionalEntry(folder) is not { } entry)
            return null;

        var build = builder.Build(manifest.Key, folder, entry);

        _conventional = (entry, build);

        return build;
    }

    public IDisposable Register(string key, string entry)
    {
        if (!string.Equals(key, manifest.Key, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"A plugin registers its client module under its own key, {manifest.Key}, not {key}.",
                nameof(key)
            );

        return Add(entry, builder.Build(manifest.Key, folder, entry));
    }

    private IDisposable Add(string entry, PluginClientBuild build)
    {
        var registration = directory.Add(
            new ClientModuleSource(
                manifest.Key,
                manifest.Version,
                Path.Combine(folder, WWWROOT),
                $"{manifest.Key}.js",
                build.File
            )
        );

        lock (_registrations)
        {
            _registrations.Add(registration);
            _registered = true;
        }

        contributions?.Add(PluginContributionKinds.CLIENT_MODULE, entry, manifest.Version);

        return registration;
    }

    /// <summary>
    /// Registers the module <see cref="BuildConventionalModule"/> built when the plugin registered
    /// none itself (a <c>TurboPlugin</c>, or a plugin with no class); answers its entry, or null.
    /// </summary>
    public string? RegisterConventionalModule()
    {
        lock (_registrations)
            if (_registered || _conventional is null)
                return null;

        var (entry, build) = _conventional.Value;

        Add(entry, build);

        return entry;
    }

    public void Dispose()
    {
        IDisposable[] registrations;

        lock (_registrations)
        {
            registrations = [.. _registrations];
            _registrations.Clear();
        }

        foreach (var registration in registrations)
            registration.Dispose();
    }
}
