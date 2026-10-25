using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Scheduling;
using Turbo.Database.Entities.Scheduling;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Operations.Scheduling;

/// <summary>
/// Registers the <see cref="IScheduledTask"/>s an assembly declares with this server's
/// <see cref="ScheduledTaskHost"/>, so a plugin's tasks run while it is loaded and go when it
/// unloads. Each is built once with the plugin's services and must be public to be found; its key
/// is <c>&lt;plugin key&gt;.&lt;name&gt;</c>.
/// </summary>
internal sealed class ScheduledTaskFeatureProcessor(ScheduledTaskHost host)
    : IAssemblyFeatureProcessor
{
    public Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        var types = AssemblyExplorer.FindAssignees(asm, typeof(IScheduledTask)).ToList();

        if (types.Count == 0)
            return Task.FromResult<IDisposable>(new CompositeDisposable());

        var owner = sp.GetService<PluginManifest>()?.Key ?? asm.GetName().Name ?? "core";
        var definitions = types
            .Select(type =>
            {
                var attribute = type.GetCustomAttribute<TaskScheduleAttribute>();
                var key = $"{owner}.{attribute?.Name ?? type.Name}";

                if (key.Length > ScheduledTaskRunEntity.KEY_MAX_LENGTH)
                    throw new InvalidOperationException(
                        $"The scheduled task {key} has a key longer than {ScheduledTaskRunEntity.KEY_MAX_LENGTH} characters; give it a shorter Name."
                    );

                return new ScheduledTaskDefinition(
                    key,
                    (IScheduledTask)ActivatorUtilities.CreateInstance(sp, type),
                    attribute
                );
            })
            .ToList();

        return Task.FromResult(host.Register(owner, definitions));
    }
}
