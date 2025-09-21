using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Registry;

internal sealed class Bucket<TContext>
{
    public readonly object Gate = new();
    public readonly List<HandlerReg<TContext>> Handlers = [];
    public readonly List<BehaviorReg<TContext>> Behaviors = [];
    public int Version;

    public int CachedVersion = -1;
    public Func<object, TContext, CancellationToken, Task>? CachedPipeline;
}
