using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Pipeline.Abstractions.Delegates;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Pipeline.Core;

public static class InvokerCache<TContext>
{
    private static readonly Type HandlerOpen = typeof(IHandler<,>);
    private static readonly Type BehaviorOpen = typeof(IBehavior<,>);

    private static readonly ConcurrentDictionary<Type, HandlerInvoker<TContext>> _h = new();
    private static readonly ConcurrentDictionary<Type, BehaviorInvoker<TContext>> _b = new();

    public static HandlerInvoker<TContext> GetHandler(Type envType) =>
        _h.GetOrAdd(envType, MakeHandler);

    public static BehaviorInvoker<TContext> GetBehavior(Type envType) =>
        _b.GetOrAdd(envType, MakeBehavior);

    private static HandlerInvoker<TContext> MakeHandler(Type envType)
    {
        var closed = HandlerOpen.MakeGenericType(envType, typeof(TContext));
        var m = closed.GetMethod("HandleAsync")!;
        var inst = Expression.Parameter(typeof(object), "i");
        var env = Expression.Parameter(typeof(object), "e");
        var ctx = Expression.Parameter(typeof(TContext), "c");
        var ct = Expression.Parameter(typeof(CancellationToken), "t");

        var p = m.GetParameters();
        static Expression Cast(Expression e, Type t) => e.Type == t ? e : Expression.Convert(e, t);

        var call = Expression.Call(
            Cast(inst, closed),
            m,
            Cast(env, p[0].ParameterType),
            Cast(ctx, p[1].ParameterType),
            Cast(ct, p[2].ParameterType)
        );

        return Expression.Lambda<HandlerInvoker<TContext>>(call, inst, env, ctx, ct).Compile();
    }

    private static BehaviorInvoker<TContext> MakeBehavior(Type envType)
    {
        var closed = BehaviorOpen.MakeGenericType(envType, typeof(TContext));
        var m = closed.GetMethod("InvokeAsync")!;
        var inst = Expression.Parameter(typeof(object), "i");
        var env = Expression.Parameter(typeof(object), "e");
        var ctx = Expression.Parameter(typeof(TContext), "c");
        var next = Expression.Parameter(typeof(Func<Task>), "n");
        var ct = Expression.Parameter(typeof(CancellationToken), "t");

        var p = m.GetParameters();
        static Expression Cast(Expression e, Type t) => e.Type == t ? e : Expression.Convert(e, t);

        var call = Expression.Call(
            Cast(inst, closed),
            m,
            Cast(env, p[0].ParameterType),
            Cast(ctx, p[1].ParameterType),
            Cast(next, p[2].ParameterType),
            Cast(ct, p[3].ParameterType)
        );

        return Expression
            .Lambda<BehaviorInvoker<TContext>>(call, inst, env, ctx, next, ct)
            .Compile();
    }
}
