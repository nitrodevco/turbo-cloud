using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Turbo.Testing;

/// <summary>One call a plugin made on a host service the test hotel stands in for.</summary>
public sealed record StandInCall(Type Interface, string Method, object?[] Args, object? Key);

/// <summary>
/// The host's services a <see cref="TestHotel"/> does not play itself (a grain, the session
/// gateway, a provider of core's): a plugin that takes one gets a stand-in that answers every call
/// with nothing (a completed task, <c>default</c>, an empty array, another stand-in for an
/// interface) and records it. A grain factory's <c>GetGrain&lt;T&gt;(key)</c> gives the stand-in
/// grain of that key. A test gives a real one with <see cref="Use{T}"/>, answers one method with
/// <see cref="Answer"/> and reads what was asked in <see cref="Calls"/>.
/// </summary>
public sealed class StandIns
{
    private readonly ConcurrentDictionary<(Type, object?), object> _instances = new();
    private readonly ConcurrentDictionary<(Type, string), Func<StandInCall, object?>> _answers =
        new();
    private readonly ConcurrentQueue<StandInCall> _calls = new();

    /// <summary>Every call made on a stand-in, in order.</summary>
    public IReadOnlyList<StandInCall> Calls => [.. _calls];

    /// <summary>The calls of <paramref name="method"/> on stand-ins of <typeparamref name="T"/>.</summary>
    public IReadOnlyList<StandInCall> CallsOf<T>(string method) =>
        [.. _calls.Where(c => c.Interface == typeof(T) && c.Method == method)];

    /// <summary>The plugin is given <paramref name="service"/> as the host's <typeparamref name="T"/>.</summary>
    public StandIns Use<T>(T service)
        where T : class
    {
        _instances[(typeof(T), null)] = service;

        return this;
    }

    /// <summary>
    /// Answers <paramref name="method"/> of <typeparamref name="T"/> stand-ins (any key) with what
    /// <paramref name="answer"/> returns: the method's own return type (a <c>Task&lt;bool&gt;</c>
    /// for an async one).
    /// </summary>
    public StandIns Answer<T>(string method, Func<StandInCall, object?> answer)
    {
        _answers[(typeof(T), method)] = answer;

        return this;
    }

    /// <summary>The stand-in of <typeparamref name="T"/> (the grain of <paramref name="key"/>).</summary>
    public T Of<T>(object? key = null)
        where T : class => (T)Of(typeof(T), key);

    internal bool Has(Type type) => _instances.ContainsKey((type, null));

    internal object Of(Type type, object? key)
    {
        if (_instances.TryGetValue((type, key), out var existing))
            return existing;

        if (!type.IsInterface)
            throw new InvalidOperationException(
                $"The test hotel has no {type.Name}, and stands in only for interfaces: give one with hotel.Host.Use(...)."
            );

        var proxy = DispatchProxy.Create(type, typeof(StandInProxy));
        var standIn = (StandInProxy)proxy;

        standIn.Owner = this;
        standIn.Interface = type;
        standIn.Key = key;

        return _instances.GetOrAdd((type, key), proxy);
    }

    internal object? Invoke(StandInCall call, MethodInfo method)
    {
        _calls.Enqueue(call);

        if (_answers.TryGetValue((call.Interface, call.Method), out var answer))
            return answer(call);

        // IGrainFactory.GetGrain<T>(key...): the stand-in grain of that key.
        if (method.Name == "GetGrain" && method.ReturnType.IsInterface)
            return Of(method.ReturnType, call.Args.Length > 0 ? call.Args[0] : null);

        return Nothing(method.ReturnType);
    }

    private object? Nothing(Type type)
    {
        if (type == typeof(void))
            return null;

        if (type == typeof(Task))
            return Task.CompletedTask;

        if (type == typeof(ValueTask))
            return ValueTask.CompletedTask;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(type.GenericTypeArguments[0])
                .Invoke(null, [Nothing(type.GenericTypeArguments[0])]);

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            return Activator.CreateInstance(type, Nothing(type.GenericTypeArguments[0]));

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
            return type.GetField(nameof(ImmutableArray<int>.Empty))!.GetValue(null);

        if (type.IsInterface && !type.IsGenericType)
            return Of(type, Guid.NewGuid());

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

/// <summary>A <see cref="StandIns"/> stand-in: every call goes to its owner.</summary>
public class StandInProxy : DispatchProxy
{
    internal StandIns Owner = default!;
    internal Type Interface = default!;
    internal object? Key;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
            return null;

        return targetMethod.Name switch
        {
            nameof(GetHashCode) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this),
            nameof(Equals) => ReferenceEquals(this, args?.FirstOrDefault()),
            nameof(ToString) => $"StandIn<{Interface.Name}>({Key})",
            _ => Owner.Invoke(
                new StandInCall(Interface, targetMethod.Name, args ?? [], Key),
                targetMethod
            ),
        };
    }
}
