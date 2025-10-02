using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Registry;

internal sealed class Bucket<TContext>
{
    public readonly object Gate = new();
    public ImmutableArray<HandlerReg<TContext>> Handlers = [];
    public ImmutableArray<BehaviorReg<TContext>> Behaviors = [];
    public int Version;
    public Func<object, TContext, CancellationToken, ValueTask>? CachedPipeline;
    public int CachedVersion;
    public Type? CachedForEnvType;
}
