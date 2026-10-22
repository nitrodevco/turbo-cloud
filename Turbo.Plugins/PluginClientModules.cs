using System;
using System.Collections.Generic;
using System.IO;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.ClientModules;

namespace Turbo.Plugins;

/// <summary>
/// The client module registry as one plugin reaches it (<see cref="IClientModuleRegistry"/>): its
/// module is served from its own folder's <c>wwwroot/</c>, at its manifest's version. What it
/// registered and did not take away itself is taken away when it unloads. Its <c>wwwroot/</c> is
/// served from the load (<see cref="ServeFiles"/>), module or not, for the files its own variables
/// name (a theme's template libraries), until it unloads.
/// </summary>
internal sealed class PluginClientModules(
    IClientModuleDirectory directory,
    PluginManifest manifest,
    string folder
) : IClientModuleRegistry, IDisposable
{
    /// <summary>The folder under a plugin's own that its client files are served from.</summary>
    public const string WWWROOT = "wwwroot";

    private readonly List<IDisposable> _registrations = [];

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

    public IDisposable Register(string key, string relativePath)
    {
        if (!string.Equals(key, manifest.Key, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"A plugin registers its client module under its own key, {manifest.Key}, not {key}.",
                nameof(key)
            );

        var registration = directory.Add(
            new ClientModuleSource(
                manifest.Key,
                manifest.Version,
                Path.Combine(folder, WWWROOT),
                relativePath
            )
        );

        lock (_registrations)
            _registrations.Add(registration);

        return registration;
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
