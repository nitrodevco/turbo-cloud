using System.Collections.Concurrent;
using System.Reflection;

namespace Turbo.Tests.Support;

/// <summary>One recorded call on a fake: which interface, which method, with what.</summary>
public sealed record FakeCall(Type Interface, string Method, object?[] Args, object? Key);

/// <summary>
/// Shared log for every fake created from one <see cref="Fakes"/> instance, so a test can see
/// the order of calls across the grain factory, grains and sessions.
/// </summary>
public sealed class CallLog
{
    public ConcurrentQueue<FakeCall> Calls { get; } = new();

    public IEnumerable<FakeCall> Of(string method) => Calls.Where(c => c.Method == method);

    public IEnumerable<FakeCall> On<TInterface>() =>
        Calls.Where(c => c.Interface == typeof(TInterface));
}

/// <summary>
/// Signature-agnostic fakes built on <see cref="DispatchProxy"/>. They answer every call with a
/// neutral value (a completed task, default, or another fake for interface-typed results) and
/// record it. Because nothing is bound to a method signature, a test keeps compiling when the
/// code under test adds a parameter to a grain method or renames an unrelated one.
/// </summary>
public sealed class Fakes
{
    public CallLog Log { get; } = new();

    /// <summary>Per-(interface, key) overrides: return a specific object for a grain lookup.</summary>
    public ConcurrentDictionary<(Type, object?), object> Instances { get; } = new();

    /// <summary>Optional behaviour hooks by method name; return <see cref="NotHandled"/> to fall through.</summary>
    public ConcurrentDictionary<string, Func<FakeCall, object?>> Handlers { get; } = new();

    public static readonly object NotHandled = new();

    public T Create<T>(object? key = null)
        where T : class => (T)Create(typeof(T), key);

    public object Create(Type iface, object? key = null)
    {
        if (Instances.TryGetValue((iface, key), out var existing))
            return existing;

        if (iface == typeof(Turbo.Primitives.Moderation.IWordFilter))
            return Instances[(iface, key)] = new PassThroughWordFilter();

        var proxy = DispatchProxy.Create(iface, typeof(RecordingProxy));
        var rp = (RecordingProxy)proxy;
        rp.Owner = this;
        rp.Interface = iface;
        rp.Key = key;
        Instances[(iface, key)] = proxy;
        return proxy;
    }
}

public class RecordingProxy : DispatchProxy
{
    internal Fakes Owner = default!;
    internal Type Interface = default!;
    internal object? Key;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
            return null;

        args ??= [];
        var call = new FakeCall(Interface, targetMethod.Name, args, Key);
        Owner.Log.Calls.Enqueue(call);

        if (targetMethod.Name == nameof(GetHashCode))
            return RuntimeHelpersHash(this);
        if (targetMethod.Name == nameof(Equals))
            return ReferenceEquals(this, args.FirstOrDefault());
        if (targetMethod.Name == nameof(ToString))
            return $"Fake<{Interface.Name}>({Key})";

        if (Owner.Handlers.TryGetValue(targetMethod.Name, out var handler))
        {
            var handled = handler(call);
            if (!ReferenceEquals(handled, Fakes.NotHandled))
                return handled;
        }

        return DefaultFor(targetMethod, args);
    }

    private static int RuntimeHelpersHash(object o) =>
        System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);

    private object? DefaultFor(MethodInfo method, object?[] args)
    {
        var rt = method.ReturnType;

        // IGrainFactory.GetGrain<T>(key...) returns a fake grain keyed by its first argument.
        if (method.Name == "GetGrain" && rt.IsInterface)
            return Owner.Create(rt, args.Length > 0 ? args[0] : null);

        return DefaultForType(rt);
    }

    internal object? DefaultForType(Type rt)
    {
        if (rt == typeof(void))
            return null;
        if (rt == typeof(Task))
            return Task.CompletedTask;
        if (rt == typeof(ValueTask))
            return ValueTask.CompletedTask;
        if (rt.IsGenericType && rt.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var inner = DefaultForType(rt.GetGenericArguments()[0]);
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(rt.GetGenericArguments()[0])
                .Invoke(null, [inner]);
        }
        if (rt.IsGenericType && rt.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var inner = DefaultForType(rt.GetGenericArguments()[0]);
            return Activator.CreateInstance(rt, inner);
        }
        if (rt.IsInterface && !rt.IsGenericType)
            return Owner.Create(rt, Guid.NewGuid());
        if (rt.IsValueType)
            return Activator.CreateInstance(rt);
        return null;
    }
}
