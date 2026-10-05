using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Orleans.Runtime;
using Turbo.Events;
using Turbo.Networking.Session;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Players.Grains;

namespace Turbo.Tests.Support;

/// <summary>
/// The real SessionGateway wired to the real PlayerPresenceGrain, with fakes only at the
/// Orleans boundary: the grain factory hands out the one presence instance, object references
/// are the observers themselves, and timers are inert. Sessions are recording fakes that
/// implement both the public session contract and the gateway's internal outbound one, so a
/// composer reaching a socket by either route is seen.
/// </summary>
public sealed class SessionHarness
{
    public Fakes Fakes { get; } = new();
    public SessionGateway Gateway { get; }

    /// <summary>The events the gateway raises, which a test can <c>Record</c>.</summary>
    public TestEventBus Events { get; } = new();

    public object Presence { get; }
    public const int PlayerId = 1;

    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public SessionHarness()
    {
        // Orleans hands back an addressable reference, not the object itself; code may compare
        // two references by grain id, so the fake reference carries one.
        Fakes.Handlers["CreateObjectReference"] = call =>
            call.Args[0] is ISessionContextObserver impl ? ObserverRef.Create(impl) : call.Args[0];
        Fakes.Handlers["DeleteObjectReference"] = _ => null;
        Fakes.Handlers["GetService"] = call =>
            call.Args[0] is Type t && t.IsInterface ? Fakes.Create(t) : null;
        Fakes.Handlers["GetRequiredService"] = call =>
            call.Args[0] is Type t && t.IsInterface ? Fakes.Create(t) : Fakes.NotHandled;

        Presence = CreatePresence();
        Fakes.Instances[(typeof(IPlayerPresenceGrain), (object)(long)PlayerId)] = Presence;
        Fakes.Instances[(typeof(IPlayerPresenceGrain), (object)PlayerId)] = Presence;

        var factory = Fakes.Create<IGrainFactory>();
        // Built by parameter type, so the harness follows the gateway's constructor as it changes.
        var ctor = typeof(SessionGateway)
            .GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        Gateway = (SessionGateway)
            ctor.Invoke(
                ctor.GetParameters()
                    .Select(p =>
                        p.ParameterType == typeof(IGrainFactory) ? factory
                        : p.ParameterType == typeof(EventSystem) ? Events.System
                        : p.ParameterType.IsGenericType
                        && p.ParameterType.GetGenericTypeDefinition().FullName
                            == "Microsoft.Extensions.Logging.ILogger`1"
                            ? Activator.CreateInstance(
                                typeof(NullLogger<>).MakeGenericType(
                                    p.ParameterType.GetGenericArguments()[0]
                                )
                            )
                        : p.ParameterType.IsInterface ? Fakes.Create(p.ParameterType)
                        : null
                    )
                    .ToArray()
            );
    }

    public IPlayerPresenceGrain PresenceGrain => (IPlayerPresenceGrain)Presence;

    private object CreatePresence()
    {
        var type = typeof(Turbo.Players.PlayerModule).Assembly.GetType(
            "Turbo.Players.Grains.PlayerPresenceGrain"
        )!;
        var grain = RuntimeHelpers.GetUninitializedObject(type);

        var stateType = type.Assembly.GetType("Turbo.Players.Grains.PlayerPresenceLiveState")!;
        var state = Activator.CreateInstance(stateType, nonPublic: true)!;
        RoomHarness.SetMember(state, "PlayerId", (Turbo.Primitives.Players.PlayerId)PlayerId);
        foreach (var p in stateType.GetProperties(All))
        {
            // Session-routing ids default to "none"; anything else keeps its initializer (which
            // an uninitialized object never ran, so give reference-typed collections a value).
            if (
                p.GetValue(state) is null
                && p.PropertyType.GetConstructor(Type.EmptyTypes) is { } c
            )
                RoomHarness.SetMember(state, p.Name, c.Invoke(null));
        }
        RoomHarness.SetField(grain, "_state", state);

        foreach (var f in type.GetFields(All))
        {
            if (f.GetValue(grain) is not null)
                continue;
            var t = f.FieldType;
            if (
                t.IsGenericType
                && t.GetGenericTypeDefinition().FullName == "Microsoft.Extensions.Logging.ILogger`1"
            )
                f.SetValue(
                    grain,
                    Activator.CreateInstance(
                        typeof(NullLogger<>).MakeGenericType(t.GetGenericArguments()[0])
                    )
                );
            else if (t.Name.EndsWith("Config", StringComparison.Ordinal) && t.IsClass)
                f.SetValue(grain, Activator.CreateInstance(t));
            else if (t == typeof(IGrainFactory))
                f.SetValue(grain, Fakes.Create<IGrainFactory>());
        }

        var context = Fakes.Create<IGrainContext>("presence-context");
        var prop = typeof(Grain).GetProperty("GrainContext", All)!;
        var setter = prop.GetSetMethod(true);
        if (setter is not null)
            setter.Invoke(grain, [context]);
        else
            typeof(Grain)
                .GetFields(All)
                .First(f => f.FieldType == typeof(IGrainContext))
                .SetValue(grain, context);

        var runtimeField = typeof(Grain).GetField("<Runtime>k__BackingField", All);
        runtimeField?.SetValue(grain, Fakes.Create<IGrainRuntime>("presence-runtime"));

        // Initialize any other live fields the class declares with initializers (queues, sets).
        foreach (var f in type.GetFields(All))
            if (
                f.GetValue(grain) is null
                && !f.FieldType.IsInterface
                && !f.FieldType.IsAbstract
                && f.FieldType.GetConstructor(Type.EmptyTypes) is { } fc
                && f.FieldType != typeof(string)
            )
                f.SetValue(grain, fc.Invoke(null));
        return grain;
    }

    /// <summary>A fake socket: its calls land in <see cref="Fakes.Log"/> keyed by the session key.</summary>
    public ISessionContext NewSession(int n)
    {
        var key = MakeKey(n);
        var session = (ISessionContext)Fakes.Create(SessionInterface, key);
        Fakes.Handlers.TryAdd("get_SessionKey", call => call.Key ?? Fakes.NotHandled);
        return session;
    }

    public static SessionKey MakeKey(int n)
    {
        var t = typeof(SessionKey);
        foreach (var ctor in t.GetConstructors())
        {
            var ps = ctor.GetParameters();
            if (ps.Length == 1 && ps[0].ParameterType == typeof(string))
                return (SessionKey)ctor.Invoke([$"test-session-{n}"]);
            if (ps.Length == 1 && ps[0].ParameterType == typeof(Guid))
                return (SessionKey)ctor.Invoke([new Guid(n, 0, 0, new byte[8])]);
        }
        var parse = t.GetMethod("Parse", [typeof(string)]) ?? t.GetMethod("From", [typeof(string)]);
        if (parse is not null)
            return (SessionKey)parse.Invoke(null, [$"test-session-{n}"])!;
        var op = t.GetMethods().FirstOrDefault(m => m.Name == "op_Implicit" && m.ReturnType == t);
        if (op is not null)
        {
            var pt = op.GetParameters()[0].ParameterType;
            object arg =
                pt == typeof(string) ? $"test-session-{n}"
                : pt == typeof(Guid) ? new Guid(n, 0, 0, new byte[8])
                : Convert.ChangeType(n, pt);
            return (SessionKey)op.Invoke(null, [arg])!;
        }
        throw new InvalidOperationException("Cannot build a SessionKey");
    }

    /// <summary>Everything that reached the socket with this key, by either route.</summary>
    public List<IComposer> Received(SessionKey key)
    {
        var list = new List<IComposer>();
        foreach (var c in Fakes.Log.Calls.Where(c => Equals(c.Key, key)))
        {
            if (c.Method is not ("SendComposerAsync" or "SendComposersAsync"))
                continue;
            foreach (var a in c.Args)
            {
                if (a is IComposer one)
                    list.Add(one);
                else if (a is IEnumerable<IComposer> many)
                    list.AddRange(many);
            }
        }
        return list;
    }

    public bool WasClosed(SessionKey key) =>
        Fakes.Log.Calls.Any(c =>
            Equals(c.Key, key) && c.Method is "CloseSessionAsync" or "CloseAsync"
        );

    public static async Task Settle()
    {
        // Presence flushes run after a Task.Yield on the thread pool.
        for (var i = 0; i < 10; i++)
            await Task.Delay(20);
    }

    private static readonly Type SessionInterface = BuildSessionInterface();

    private static Type BuildSessionInterface()
    {
        var outbound = typeof(SessionGateway).Assembly.GetType(
            "Turbo.Networking.Session.ISessionOutbound"
        );
        if (outbound is null)
            return typeof(ISessionContext);

        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("TurboTestsSessionDyn"),
            AssemblyBuilderAccess.Run
        );
        var mb = ab.DefineDynamicModule("TurboTestsSessionDyn");

        // IgnoresAccessChecksTo lets this dynamic assembly name an internal interface.
        var attr = mb.DefineType(
            "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(Attribute)
        );
        var ctor = attr.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            [typeof(string)]
        );
        var il = ctor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(All, Type.EmptyTypes)!);
        il.Emit(OpCodes.Ret);
        var attrType = attr.CreateType()!;
        ab.SetCustomAttribute(
            new CustomAttributeBuilder(
                attrType.GetConstructor([typeof(string)])!,
                ["Turbo.Networking"]
            )
        );

        var tb = mb.DefineType(
            "ITestSession",
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract,
            null,
            [typeof(ISessionContext), outbound]
        );
        return tb.CreateType()!;
    }
}

/// <summary>An observer reference as Orleans would give one: forwards calls, has a grain id.</summary>
public class ObserverRef : DispatchProxy
{
    private static int _next;
    private ISessionContextObserver _target = default!;
    private IGrainContext _context = default!;

    public ISessionContextObserver Target => _target;

    public static ISessionContextObserver Create(ISessionContextObserver target)
    {
        var proxy = DispatchProxy.Create(RefInterface.Value, typeof(ObserverRef));
        var self = (ObserverRef)(object)proxy;
        self._target = target;
        var id = GrainId.Create(
            GrainType.Create("sys.client"),
            IdSpan.Create($"observer-{Interlocked.Increment(ref _next)}")
        );
        self._context = GrainContextStub.Create(id);
        return (ISessionContextObserver)proxy;
    }

    // Emitted at runtime: Orleans' source generator rejects a compiled interface that inherits
    // IGrainBase's property.
    private static readonly Lazy<Type> RefInterface = new(() =>
    {
        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("TurboTestsObserverRefDyn"),
            AssemblyBuilderAccess.Run
        );
        var tb = ab.DefineDynamicModule("TurboTestsObserverRefDyn")
            .DefineType(
                "ITestObserverRef",
                TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract,
                null,
                [typeof(ISessionContextObserver), typeof(IGrainBase)]
            );
        return tb.CreateType()!;
    });

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
            return null;
        if (targetMethod.Name == "get_GrainContext")
            return _context;
        if (
            targetMethod.DeclaringType == typeof(object)
            || targetMethod.Name is "Equals" or "GetHashCode" or "ToString"
        )
            return targetMethod.Name switch
            {
                "Equals" => ReferenceEquals(this, args?[0]),
                "GetHashCode" => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this),
                _ => $"ObserverRef({_target})",
            };
        if (targetMethod.DeclaringType!.IsAssignableFrom(_target.GetType()))
            return targetMethod.Invoke(_target, args);
        if (targetMethod.ReturnType == typeof(Task))
            return Task.CompletedTask;
        return null;
    }
}

public class GrainContextStub : DispatchProxy
{
    private GrainId _id;

    public static IGrainContext Create(GrainId id)
    {
        var proxy = DispatchProxy.Create<IGrainContext, GrainContextStub>();
        ((GrainContextStub)(object)proxy)._id = id;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod?.Name switch
        {
            "get_GrainId" => _id,
            "Equals" => ReferenceEquals(this, args?[0]),
            "GetHashCode" => _id.GetHashCode(),
            _ => targetMethod?.ReturnType is { IsValueType: true } t && t != typeof(void)
                ? Activator.CreateInstance(t)
                : null,
        };
}
