using System.Collections.Immutable;

namespace Turbo.Pipeline.Registry;

internal sealed class Bucket<TContext>
{
    public readonly object Gate = new();
    public ImmutableArray<HandlerReg<TContext>> Handlers = [];
    public ImmutableArray<BehaviorReg<TContext>> Behaviors = [];
}
