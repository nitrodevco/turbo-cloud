using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Exports;
using Turbo.Primitives.ClientModules;

namespace Skills;

/// <summary>
/// The plugin: its services and settings and its client module (<c>wwwroot/skills.js</c>, built
/// from <c>client/src</c>). Its command, permission nodes, messages and their handlers are found in
/// the assembly as it loads; everything it registers goes when it unloads.
/// </summary>
public sealed class SkillsPlugin : ITurboPlugin
{
    private readonly List<IDisposable> _registrations = [];

    public string Key => "skills";

    public string Version => "0.1.0";

    public void ConfigureServices(IServiceCollection services, PluginManifest manifest)
    {
        services.AddOptions<SkillsSettings>().BindConfiguration(SkillsSettings.SECTION_NAME);
        services.AddSingleton<SkillBook>();
    }

    public Task StartAsync(IServiceProvider services, CancellationToken ct)
    {
        _registrations.Add(
            services
                .GetRequiredService<IHostServices>()
                .GetRequiredService<IClientModuleRegistry>()
                .Register(Key, "skills.js")
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        foreach (var registration in _registrations)
            registration.Dispose();

        _registrations.Clear();

        return Task.CompletedTask;
    }

    public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
