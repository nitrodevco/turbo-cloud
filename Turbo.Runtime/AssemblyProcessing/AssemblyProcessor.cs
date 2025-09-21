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
        var tasks = _processors.Select(p => p.ProcessAsync(asm, sp, ct)).ToArray();

        try
        {
            var regs = await Task.WhenAll(tasks).ConfigureAwait(false);

            return new CompositeDisposable(regs.AsEnumerable().Reverse());
        }
        catch
        {
            foreach (var t in tasks)
                if (t.IsCompletedSuccessfully)
                    t.Result.Dispose();

            throw;
        }
    }
}
