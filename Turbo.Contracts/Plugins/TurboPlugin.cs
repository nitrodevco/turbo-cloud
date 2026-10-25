using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins.Exports;

namespace Turbo.Contracts.Plugins;

/// <summary>
/// The plugin class by convention: everything an <see cref="ITurboPlugin"/> spells out is done by
/// the host, so a plugin writes only what it adds. Its key and version are its manifest's; its
/// <c>wwwroot/&lt;key&gt;.js</c> is registered as its client module, its
/// <see cref="PluginSettingsAttribute"/> classes are bound and its <c>PluginDbContextBase</c>
/// contexts get their tables, without a line here. What it registers by hand in
/// <see cref="StartingAsync"/> it hands to <see cref="IPluginContext.Track{T}"/>, and it goes
/// when the plugin stops.
/// <para>
/// A plugin with nothing to add has no class at all: an assembly without one loads as this base
/// with no overrides (commands, messages, handlers, routes... are found in it as always).
/// </para>
/// </summary>
public abstract class TurboPlugin : ITurboPlugin
{
    private readonly List<IDisposable> _tracked = [];
    private PluginManifest? _manifest;
    private IPluginContext? _context;

    /// <summary>The plugin's manifest, as the host read it.</summary>
    public PluginManifest Manifest =>
        _manifest
        ?? throw new InvalidOperationException(
            $"{GetType().Name} is read before the host gave it its manifest."
        );

    public string Key => Manifest.Key;

    public string Version => Manifest.Version;

    /// <summary>Called by the host before anything else is asked of the plugin.</summary>
    internal void UseManifest(PluginManifest manifest) => _manifest = manifest;

    /// <summary>The plugin's own services: what its classes take by constructor.</summary>
    protected virtual void Services(IServiceCollection services) { }

    /// <summary>
    /// After its services are built, its tables migrated and its hosted services started; its
    /// web routes and admin pages answer once it returns.
    /// </summary>
    protected virtual Task StartingAsync(IPluginContext context, CancellationToken ct) =>
        Task.CompletedTask;

    /// <summary>As it stops, before what it tracked is disposed.</summary>
    protected virtual Task StoppingAsync(IPluginContext context, CancellationToken ct) =>
        Task.CompletedTask;

    /// <inheritdoc cref="ITurboPlugin.BindExportsAsync"/>
    protected virtual Task BindExportsAsync(IExportBinder binder, IServiceProvider services) =>
        Task.CompletedTask;

    void ITurboPlugin.ConfigureServices(IServiceCollection services, PluginManifest manifest)
    {
        _manifest ??= manifest;
        Services(services);
    }

    Task ITurboPlugin.StartAsync(IServiceProvider services, CancellationToken ct)
    {
        _context = new Context(this, services);

        return StartingAsync(_context, ct);
    }

    async Task ITurboPlugin.StopAsync(CancellationToken ct)
    {
        try
        {
            if (_context is not null)
                await StoppingAsync(_context, ct).ConfigureAwait(false);
        }
        finally
        {
            DisposeTracked();
            _context = null;
        }
    }

    Task ITurboPlugin.BindExportsAsync(IExportBinder binder, IServiceProvider services) =>
        BindExportsAsync(binder, services);

    public virtual ValueTask DisposeAsync()
    {
        DisposeTracked();
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    private void DisposeTracked()
    {
        IDisposable[] tracked;

        lock (_tracked)
        {
            tracked = [.. _tracked];
            _tracked.Clear();
        }

        // Last tracked first, as a using block would.
        for (var i = tracked.Length - 1; i >= 0; i--)
            tracked[i].Dispose();
    }

    private sealed class Context(TurboPlugin plugin, IServiceProvider services) : IPluginContext
    {
        public PluginManifest Manifest => plugin.Manifest;

        public IServiceProvider Services => services;

        public T Host<T>()
            where T : notnull =>
            services.GetRequiredService<IHostServices>().GetRequiredService<T>();

        public T Track<T>(T registration)
            where T : IDisposable
        {
            lock (plugin._tracked)
                plugin._tracked.Add(registration);

            return registration;
        }
    }
}
