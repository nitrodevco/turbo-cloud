using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;

namespace Skills;

/// <summary>
/// The plugin's services. The rest is found as it loads: its key and version in manifest.json, its
/// settings, command, permission nodes, messages and handlers in the assembly, and its client
/// module, built by the hotel from client/src. All of it goes when it unloads.
/// </summary>
public sealed class SkillsPlugin : TurboPlugin
{
    protected override void Services(IServiceCollection services) =>
        services.AddSingleton<SkillBook>();
}
