using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Pipeline.Delegates;
using Turbo.Pipeline.Registry;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Pipeline;

public class GenericInvokerFactory<TContext>
{
    public HandlerInvoker<TContext> CreateHandlerInvoker(Type handlerType, Type envType)
    {
        var impl = AssemblyExplorer.ResolveImplementation(
            handlerType,
            typeof(IHandler<,>).MakeGenericType(envType, typeof(TContext)),
            "HandleAsync"
        );

        var inst = Expression.Parameter(typeof(object), "inst");
        var env = Expression.Parameter(typeof(object), "env");
        var ctx = Expression.Parameter(typeof(TContext), "ctx");
        var ct = Expression.Parameter(typeof(CancellationToken), "ct");

        var call = Expression.Call(
            Expression.Convert(inst, handlerType),
            impl,
            Expression.Convert(env, envType),
            ctx,
            ct
        );

        return Expression.Lambda<HandlerInvoker<TContext>>(call, inst, env, ctx, ct).Compile();
    }

    public BehaviorInvoker<TContext> CreateBehaviorInvoker(Type behaviorType, Type envType)
    {
        var impl = AssemblyExplorer.ResolveImplementation(
            behaviorType,
            typeof(IBehavior<,>).MakeGenericType(envType, typeof(TContext)),
            "InvokeAsync"
        );

        var inst = Expression.Parameter(typeof(object), "inst");
        var env = Expression.Parameter(typeof(object), "env");
        var ctx = Expression.Parameter(typeof(TContext), "ctx");
        var next = Expression.Parameter(typeof(Func<Task>), "next");
        var ct = Expression.Parameter(typeof(CancellationToken), "ct");

        var call = Expression.Call(
            Expression.Convert(inst, behaviorType),
            impl,
            Expression.Convert(env, envType),
            ctx,
            next,
            ct
        );

        return Expression
            .Lambda<BehaviorInvoker<TContext>>(call, inst, env, ctx, next, ct)
            .Compile();
    }
}
