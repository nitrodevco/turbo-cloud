using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Commands;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Commands;

/// <summary>
/// Registers the <see cref="ICommand"/>s an assembly declares, so a plugin's commands exist while
/// it is loaded and go when it unloads, as its handlers and permission sources do. A command is
/// built with the plugin's services, and must be public to be found.
/// </summary>
internal sealed class CommandFeatureProcessor(
    ICommandRegistryProvider registryProvider,
    SuggestionSourceRegistry suggestionSources,
    CommandArgumentParserRegistry argumentParsers
) : IAssemblyFeatureProcessor
{
    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var commands = AssemblyExplorer
            .FindAssignees(asm, typeof(ICommand))
            .Select(concrete => (ICommand)ActivatorUtilities.CreateInstance(sp, concrete))
            .ToList();

        var sources = AssemblyExplorer
            .FindAssignees(asm, typeof(ISuggestionSource))
            .Select(concrete => (ISuggestionSource)ActivatorUtilities.CreateInstance(sp, concrete))
            .ToList();

        var registrations = new CompositeDisposable();

        var parsers = AssemblyExplorer
            .FindAssignees(asm, typeof(ICommandArgumentParser))
            .Select(concrete =>
                (ICommandArgumentParser)ActivatorUtilities.CreateInstance(sp, concrete)
            )
            .ToList();
        registrations.Add(argumentParsers.Register(parsers));

        // Sources first, so a command that names one never finds it missing.
        try
        {
            if (sources.Count > 0)
                registrations.Add(suggestionSources.Register(sources));
            if (commands.Count > 0)
                registrations.Add(registryProvider.Register(commands));
        }
        catch
        {
            // The commands clashed: the plugin fails to load, so its sources go too.
            registrations.Dispose();

            throw;
        }

        return Task.FromResult<IDisposable>(registrations);
    }
}
