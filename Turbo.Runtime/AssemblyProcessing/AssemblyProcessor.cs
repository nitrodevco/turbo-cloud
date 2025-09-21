using System;
using System.Collections.Generic;
using System.Reflection;

namespace Turbo.Runtime.AssemblyProcessing;

public sealed class AssemblyProcessor(IEnumerable<IAssemblyFeatureProcessor> processors)
{
    private readonly IReadOnlyList<IAssemblyFeatureProcessor> _processors = [.. processors];

    public IDisposable Process(Assembly asm, IServiceProvider pluginServices)
    {
        var batch = new CompositeDisposable();

        foreach (var p in _processors)
            batch.Add(p.Process(asm, pluginServices));

        return batch;
    }
}
