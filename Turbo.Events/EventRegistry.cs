using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Events.Abstractions.Attributes;
using Turbo.Events.Abstractions.Registry;
using Turbo.Events.Registry;

namespace Turbo.Events;

public class EventRegistry
{
    private readonly Dictionary<Type, List<Handler>> _handlers = [];
    private readonly Dictionary<Type, List<Behavior>> _behaviors = [];

    public static EventRegistry Build(IEnumerable<Assembly> assemblies)
    {
        var reg = new EventRegistry();

        var types = assemblies
            .SelectMany(SafeGetTypes)
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .ToArray();

        foreach (var t in types)
        {
            foreach (var i in t.GetInterfaces())
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                {
                    var evt = i.GenericTypeArguments[0];
                    var order = t.GetCustomAttribute<OrderAttribute>()?.Value ?? 0;
                    var tags = t.GetCustomAttributes<EventTagAttribute>()
                        .Select(x => x.Tag)
                        .ToArray();
                    var method = i.GetMethods()
                        .First(m =>
                        {
                            if (m.Name != nameof(IEventHandler<object>.HandleAsync))
                                return false;
                            var ps = m.GetParameters();
                            return ps.Length == 3
                                && ps[0].ParameterType.IsAssignableFrom(evt) // TEvent
                                && ps[1].ParameterType == typeof(EventContext)
                                && ps[2].ParameterType == typeof(CancellationToken);
                        });
                    var inv = BuildHandlerInvoker(method, i, t);
                    if (!reg._handlers.TryGetValue(evt, out var list))
                        reg._handlers[evt] = list = new();
                    list.Add(new Handler(i, inv, order, tags));
                }
                else if (
                    i.IsGenericType
                    && i.GetGenericTypeDefinition() == typeof(IEventBehavior<>)
                )
                {
                    var evt = i.GenericTypeArguments[0];
                    var order = t.GetCustomAttribute<OrderAttribute>()?.Value ?? 0;
                    var tags = t.GetCustomAttributes<EventTagAttribute>()
                        .Select(x => x.Tag)
                        .ToArray();
                    var method = i.GetMethods()
                        .First(m =>
                        {
                            if (m.Name != nameof(IEventBehavior<object>.InvokeAsync))
                                return false;
                            var p = m.GetParameters();
                            return p.Length == 4
                                && p[0].ParameterType.IsAssignableFrom(evt) // TEvent
                                && p[1].ParameterType == typeof(EventContext)
                                && p[2].ParameterType == typeof(Func<Task>)
                                && p[3].ParameterType == typeof(CancellationToken);
                        });
                    var inv = BuildBehaviorInvoker(t, i, method);
                    if (!reg._behaviors.TryGetValue(evt, out var list))
                        reg._behaviors[evt] = list = new();
                    list.Add(new Behavior(inv, order, tags));
                }
            }
        }

        foreach (var k in reg._handlers.Keys.ToList())
            reg._handlers[k] = [.. reg._handlers[k].OrderBy(h => h.Order)];
        foreach (var k in reg._behaviors.Keys.ToList())
            reg._behaviors[k] = [.. reg._behaviors[k].OrderBy(b => b.Order)];
        return reg;

        static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try
            {
                return a.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t is not null)!;
            }
        }

        static Func<object, object, EventContext, CancellationToken, Task> BuildHandlerInvoker(
            MethodInfo method,
            Type iface,
            Type impl
        ) => (instance, evt, ctx, ct) => (Task)method.Invoke(instance, [evt, ctx, ct])!;

        static Func<
            IServiceProvider,
            object,
            EventContext,
            Func<Task>,
            CancellationToken,
            Task
        > BuildBehaviorInvoker(Type impl, Type iface, MethodInfo method) =>
            (sp, evt, ctx, next, ct) =>
            {
                var b = sp.GetRequiredService(iface);
                return (Task)method.Invoke(b, [evt, ctx, next, ct])!;
            };
    }

    public (IReadOnlyList<Handler> Handlers, IReadOnlyList<Behavior> Behaviors) Get(
        Type evtType,
        string? tag
    )
    {
        _handlers.TryGetValue(evtType, out var hs);
        _behaviors.TryGetValue(evtType, out var bs);
        var handlers = (hs ?? []).Where(h => tag is null || h.Tags.Contains(tag)).ToList();
        var behaviors = (bs ?? []).Where(b => tag is null || b.Tags.Contains(tag)).ToList();
        return (handlers, behaviors);
    }
}
