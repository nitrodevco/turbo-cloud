using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Runtime.AssemblyProcessing;

public sealed class AssemblyProcessor(IEnumerable<IAssemblyFeatureProcessor> processors)
{
    private readonly IReadOnlyList<IAssemblyFeatureProcessor> _processors = [.. processors];

    public async Task<IDisposable> ProcessAsync(
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct = default
    )
    {
        // A processor that throws before it returns a task (building a command whose service
        // can't be resolved) fails like one whose task faults, so what the others registered is
        // taken away and a later load of the assembly does not find it registered twice.
        var tasks = _processors.Select(p => RunAsync(p, asm, sp, ct)).ToArray();

        try
        {
            var regs = await Task.WhenAll(tasks).ConfigureAwait(false);

            return new CompositeDisposable(regs.AsEnumerable().Reverse());
        }
        catch
        {
            foreach (var t in tasks)
                if (t.IsCompletedSuccessfully)
                    (await t.ConfigureAwait(false)).Dispose();

            throw;
        }
    }

    private static Task<IDisposable> RunAsync(
        IAssemblyFeatureProcessor processor,
        Assembly asm,
        IServiceProvider sp,
        CancellationToken ct
    )
    {
        try
        {
            return processor.ProcessAsync(asm, sp, ct);
        }
        catch (Exception ex)
        {
            return Task.FromException<IDisposable>(ex);
        }
    }
}
