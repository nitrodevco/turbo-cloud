using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.Games;
using Turbo.Primitives.Plugins;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms.Games;

/// <summary>
/// Registers the <see cref="IRoomGame"/>s, <see cref="IGameRewardHandler"/>s and
/// <see cref="IGameAccessPolicy"/> an assembly declares, so a plugin's games are in the game
/// centre while it is loaded. Each is built once with the plugin's services and must be public to
/// be found; one that is <see cref="IDisposable"/> is disposed when the plugin unloads. An access
/// policy replaces the server's own in its slot (<c>docs/plugin-services.md</c>), which is skipped.
/// </summary>
internal sealed class GameFeatureProcessor(GameRegistry registry, IReplaceableServices services)
    : IAssemblyFeatureProcessor
{
    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var games = Build<IRoomGame>(asm, sp);
        var handlers = Build<IGameRewardHandler>(asm, sp);
        var policies = Build<IGameAccessPolicy>(asm, sp);

        var registrations = new CompositeDisposable();

        if (games.Count == 0 && handlers.Count == 0 && policies.Count == 0)
            return Task.FromResult<IDisposable>(registrations);

        foreach (var game in games)
            sp.Contributes(PluginContributionKinds.GAME, game.GetType().Name);

        foreach (var policy in policies)
            sp.Contributes(
                PluginContributionKinds.SERVICE,
                nameof(IGameAccessPolicy),
                policy.GetType().Name
            );

        var owner = sp.GetService<PluginManifest>()?.Key ?? asm.GetName().Name ?? "core";

        // Disposed in reverse: the registration goes first, so nothing reaches a game as it is
        // being disposed.
        registrations.Add(games.OfType<IDisposable>());
        registrations.Add(handlers.OfType<IDisposable>());
        registrations.Add(policies.OfType<IDisposable>());
        registrations.Add(registry.Register(games, handlers));

        foreach (var policy in policies)
            registrations.Add(services.Replace(typeof(IGameAccessPolicy), owner, policy));

        return Task.FromResult<IDisposable>(registrations);
    }

    private static List<T> Build<T>(Assembly asm, IServiceProvider sp) =>
        AssemblyExplorer
            .FindAssignees(asm, typeof(T))
            .Where(concrete => concrete != typeof(DefaultGameAccessPolicy))
            .Where(concrete => !concrete.IsDefined(typeof(ManualGameRegistrationAttribute), false))
            .Select(concrete => (T)ActivatorUtilities.CreateInstance(sp, concrete))
            .ToList();
}
