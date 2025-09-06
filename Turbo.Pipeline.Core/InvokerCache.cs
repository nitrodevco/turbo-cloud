using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Pipeline.Abstractions.Delegates;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Pipeline.Core;

public static class InvokerCache
{
    private static readonly ConcurrentDictionary<
        (Type iface, Type impl),
        HandlerInvoker
    > HANDLER_INVOKERS = new();

    private static readonly ConcurrentDictionary<
        (Type iface, Type impl),
        BehaviorInvoker
    > BEHAVIOR_INVOKERS = new();

    /// <summary>
    /// Get a compiled invoker for IEventHandler&lt;T&gt;.HandleAsync(...)
    /// closedInterface: e.g., typeof(IEventHandler&lt;MyEvent&gt;)
    /// implType: the concrete handler type implementing that interface
    /// </summary>
    public static HandlerInvoker GetHandlerInvoker(Type closedInterface, Type implType) =>
        HANDLER_INVOKERS.GetOrAdd((closedInterface, implType), BuildHandlerInvoker);

    /// <summary>
    /// Get a compiled invoker for IEventBehavior&lt;T&gt;.InvokeAsync(...)
    /// closedInterface: e.g., typeof(IEventBehavior&lt;MyEvent&gt;)
    /// implType: the concrete behavior type implementing that interface
    /// </summary>
    public static BehaviorInvoker GetBehaviorInvoker(Type closedInterface, Type implType) =>
        BEHAVIOR_INVOKERS.GetOrAdd((closedInterface, implType), BuildBehaviorInvoker);

    // ------------------------ builders ------------------------

    private static HandlerInvoker BuildHandlerInvoker((Type iface, Type impl) key)
    {
        var (iface, impl) = key;
        var msgType = iface.GetGenericArguments()[0];

        var ifaceMethod = FindIfaceHandlerMethod(iface, msgType);
        var target = ResolveTargetMethod(impl, iface, ifaceMethod);

        var handlerParam = Expression.Parameter(typeof(object), "handler");
        var msgParam = Expression.Parameter(typeof(object), "message");
        var ctxParam = Expression.Parameter(typeof(PipelineContext), "context");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var parms = target.GetParameters(); // [TMsg, <ctxType>, CancellationToken]
        var call = Expression.Call(
            Expression.Convert(handlerParam, impl),
            target,
            ConvertArg(msgParam, msgType),
            ConvertArg(ctxParam, parms[1].ParameterType), // <-- adapts PipelineContext -> EventContext
            ConvertArg(ctParam, parms[2].ParameterType)
        );

        return Expression
            .Lambda<HandlerInvoker>(call, handlerParam, msgParam, ctxParam, ctParam)
            .Compile();
    }

    private static BehaviorInvoker BuildBehaviorInvoker((Type iface, Type impl) key)
    {
        var (iface, impl) = key;
        var msgType = iface.GetGenericArguments()[0];

        var ifaceMethod = FindIfaceBehaviorMethod(iface, msgType);
        var target = ResolveTargetMethod(impl, iface, ifaceMethod);

        var handlerParam = Expression.Parameter(typeof(object), "handler");
        var msgParam = Expression.Parameter(typeof(object), "message");
        var ctxParam = Expression.Parameter(typeof(PipelineContext), "context");
        var nextParam = Expression.Parameter(typeof(Func<Task>), "next");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var parms = target.GetParameters(); // [TMsg, <ctxType>, Func<Task>, CancellationToken]
        var call = Expression.Call(
            Expression.Convert(handlerParam, impl),
            target,
            ConvertArg(msgParam, msgType),
            ConvertArg(ctxParam, parms[1].ParameterType), // <-- adapts PipelineContext -> EventContext
            ConvertArg(nextParam, parms[2].ParameterType),
            ConvertArg(ctParam, parms[3].ParameterType)
        );

        return Expression
            .Lambda<BehaviorInvoker>(call, handlerParam, msgParam, ctxParam, nextParam, ctParam)
            .Compile();
    }

    // ------------------------ helpers ------------------------

    private static MethodInfo ResolveTargetMethod(Type impl, Type iface, MethodInfo ifaceMethod)
    {
        // Works for explicit & implicit interface implementations
        var map = impl.GetInterfaceMap(iface);
        var idx = Array.IndexOf(map.InterfaceMethods, ifaceMethod);
        if (idx < 0)
            throw new MissingMethodException(
                $"{impl} does not implement {ifaceMethod.DeclaringType?.Name}.{ifaceMethod.Name}"
            );
        return map.TargetMethods[idx];
    }

    private static Expression ConvertArg(Expression expr, Type toType)
    {
        // If already assignable, no-op; else add a Convert
        return toType.IsAssignableFrom(expr.Type) ? expr : Expression.Convert(expr, toType);
    }

    private static MethodInfo FindIfaceHandlerMethod(Type iface, Type msgType)
    {
        // Look for HandleAsync(TMsg, <any ctx>, CancellationToken)
        // Prefer exact match on first param; third must be CancellationToken; arity = 3
        return iface
                .GetMethods()
                .Where(m => m.Name == "HandleAsync")
                .Select(m => new { m, p = m.GetParameters() })
                .Where(x => x.p.Length == 3)
                .Where(x => x.p[0].ParameterType == msgType)
                .Where(x => x.p[2].ParameterType == typeof(CancellationToken))
                .Select(x => x.m)
                .SingleOrDefault()
            ?? throw new MissingMethodException(
                $"{iface} missing HandleAsync({msgType}, *, CancellationToken)"
            );
    }

    private static MethodInfo FindIfaceBehaviorMethod(Type iface, Type msgType)
    {
        // Look for InvokeAsync(TMsg, <any ctx>, Func<Task>, CancellationToken)
        return iface
                .GetMethods()
                .Where(m => m.Name == "InvokeAsync")
                .Select(m => new { m, p = m.GetParameters() })
                .Where(x => x.p.Length == 4)
                .Where(x => x.p[0].ParameterType == msgType)
                .Where(x => x.p[2].ParameterType == typeof(Func<Task>))
                .Where(x => x.p[3].ParameterType == typeof(CancellationToken))
                .Select(x => x.m)
                .SingleOrDefault()
            ?? throw new MissingMethodException(
                $"{iface} missing InvokeAsync({msgType}, *, Func<Task>, CancellationToken)"
            );
    }
}
