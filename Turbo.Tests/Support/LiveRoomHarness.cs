using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Grains;

namespace Turbo.Tests.Support;

/// <summary>
/// A RoomGrain built by its real constructor, so every module and system is wired, and every
/// event listener registered, exactly as the code under test does it. The constructor runs under
/// a fake Orleans execution context (grain id "room/1"); logic types are registered the way the
/// host does at startup (the [RoomObjectLogic] assembly scan) into a real
/// RoomObjectLogicProvider over a real service collection. Grain calls, the database and the
/// room stream are fakes.
/// </summary>
public sealed class LiveRoomHarness
{
    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public Fakes Fakes { get; } = new();
    public RoomGrain Room { get; }
    public object State { get; }
    public IServiceProvider Services { get; }
    public IRoomObjectLogicProvider LogicProvider { get; }

    public LiveRoomHarness(int width = 10, int height = 10)
    {
        Fakes.Handlers["GetService"] = call =>
            call.Args[0] is Type t && t.IsInterface && !t.IsGenericType ? Fakes.Create(t) : null;
        Fakes.Handlers["get_GrainId"] = _ =>
            GrainId.Create(GrainType.Create("room"), GrainIdKeyExtensions.CreateIntegerKey(1));
        Fakes.Handlers["GetPlayerNameAsync"] = _ => Task.FromResult<string?>("test");

        Services = BuildServices();
        LogicProvider = (IRoomObjectLogicProvider)
            ActivatorUtilities.CreateInstance(
                Services,
                typeof(RoomGrain).Assembly.GetType("Turbo.Rooms.Providers.RoomObjectLogicProvider")!
            );
        RegisterLogics();

        Room = (RoomGrain)RuntimeHelpers.GetUninitializedObject(typeof(RoomGrain));
        var context = Fakes.Create<IGrainContext>("room-context");
        var rc = typeof(IGrainContext).Assembly.GetType("Orleans.Runtime.RuntimeContext")!;
        var set = rc.GetMethod(
            "SetExecutionContext",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public
        )!;
        var reset = rc.GetMethod(
            "ResetExecutionContext",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public
        )!;
        var setArgs = new object?[] { context, null };
        set.Invoke(null, setArgs);
        try
        {
            var ctor = typeof(RoomGrain)
                .GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .First();
            var args = ctor.GetParameters().Select(p => ResolveCtorArg(p.ParameterType)).ToArray();
            ctor.Invoke(Room, args);
        }
        finally
        {
            reset.Invoke(null, [setArgs[1]]);
        }
        typeof(Grain)
            .GetField("<Runtime>k__BackingField", All)
            ?.SetValue(Room, Fakes.Create<IGrainRuntime>("room-runtime"));
        State = RoomHarness.GetField(Room, "_state")!;

        // The room stream everyone in the room listens to.
        var streamField = typeof(RoomGrain).GetField("_roomOutbound", All);
        streamField?.SetValue(Room, Fakes.Create(streamField.FieldType, "room-stream"));

        var size = width * height;
        RoomHarness.SetMember(
            State,
            "Model",
            new RoomModelSnapshot
            {
                Id = 1,
                Name = "test",
                Model = "",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Width = width,
                Height = height,
                Size = size,
                BaseHeights = new Altitude[size],
                BaseFlags = Enumerable.Repeat(RoomTileFlags.Open, size).ToArray(),
            }
        );
        var snap = (RoomSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomSnapshot));
        RoomHarness.SetMember(snap, "RoomId", (RoomId)1);
        RoomHarness.SetMember(snap, "OwnerId", (Turbo.Primitives.Players.PlayerId)1);
        RoomHarness.SetMember(snap, "Name", "test");
        RoomHarness.SetMember(State, "RoomSnapshot", snap);
        var map = RoomHarness.GetField(Room, "MapModule")!;
        var ensure = map.GetType().GetMethod("EnsureMapBuiltAsync", All);
        if (ensure?.Invoke(map, [default(CancellationToken)]) is Task t)
            t.GetAwaiter().GetResult();
    }

    /// <summary>Every logic key the startup scan registered in this workspace.</summary>
    public IReadOnlyCollection<string> RegisteredLogicKeys =>
        LogicProvider
            .GetType()
            .GetFields(All)
            .Select(f => f.GetValue(LogicProvider))
            .OfType<System.Collections.IDictionary>()
            .SelectMany(d => d.Keys.OfType<string>())
            .ToHashSet();

    public T Module<T>()
        where T : class =>
        (T)typeof(RoomGrain).GetFields(All).First(f => f.FieldType == typeof(T)).GetValue(Room)!;

    private IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<IGrainFactory>(Fakes.Create<IGrainFactory>());
        var furniture = typeof(Turbo.Furniture.Providers.StuffDataFactory).Assembly;
        services.AddSingleton(
            typeof(Turbo.Primitives.Furniture.Providers.IStuffDataFactory),
            typeof(Turbo.Furniture.Providers.StuffDataFactory)
        );
        // Anything else a logic asks for resolves to a recording fake.
        return new FallbackProvider(services.BuildServiceProvider(), Fakes);
    }

    private void RegisterLogics()
    {
        var processorType = typeof(RoomGrain).Assembly.GetType(
            "Turbo.Rooms.Object.Logic.RoomObjectLogicFeatureProcessor"
        )!;
        var processor = Activator.CreateInstance(processorType, All, null, [LogicProvider], null)!;
        var process = processorType.GetMethod("ProcessAsync")!;
        foreach (var asm in new[] { typeof(RoomGrain).Assembly })
            ((Task)process.Invoke(processor, [asm, Services, CancellationToken.None])!)
                .GetAwaiter()
                .GetResult();
    }

    private object? ResolveCtorArg(Type t)
    {
        if (t == typeof(IRoomObjectLogicProvider))
            return LogicProvider;
        if (
            t.IsGenericType
            && t.GetGenericTypeDefinition().FullName == "Microsoft.Extensions.Options.IOptions`1"
        )
        {
            var inner = Activator.CreateInstance(t.GetGenericArguments()[0])!;
            return typeof(Microsoft.Extensions.Options.Options)
                .GetMethod("Create")!
                .MakeGenericMethod(t.GetGenericArguments()[0])
                .Invoke(null, [inner]);
        }
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ILogger<>))
            return Activator.CreateInstance(
                typeof(NullLogger<>).MakeGenericType(t.GetGenericArguments()[0])
            );
        if (t.IsGenericType && t.Name.StartsWith("IDbContextFactory"))
            return new InMemoryDb();
        if (t.IsInterface)
            return Fakes.Create(t);
        return RuntimeHelpers.GetUninitializedObject(t);
    }

    private sealed class FallbackProvider(IServiceProvider inner, Fakes fakes)
        : IServiceProvider,
            ISupportRequiredService
    {
        public object? GetService(Type serviceType) =>
            inner.GetService(serviceType)
            ?? (
                serviceType.IsInterface && !serviceType.IsGenericType
                    ? fakes.Create(serviceType)
                    : null
            );

        public object GetRequiredService(Type serviceType) =>
            GetService(serviceType)
            ?? throw new InvalidOperationException($"No service {serviceType}");
    }
}
