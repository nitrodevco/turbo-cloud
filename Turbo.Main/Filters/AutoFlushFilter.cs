using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Grains.Shared;

namespace Turbo.Main.Filters;

public sealed class AutoFlushFilter : IIncomingGrainCallFilter
{
    public async Task Invoke(IIncomingGrainCallContext context)
    {
        await context.Invoke(); // run the grain method first

        if (context.Grain is IAutoFlushGrain g && g.IsDirty)
        {
            await g.FlushExternalAsync(CancellationToken.None);
        }
    }
}