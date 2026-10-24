using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Furniture.Providers;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Furniture.Floor;

namespace Turbo.Tests.Support;

/// <summary>
/// A <see cref="RoomGrain"/> with no Orleans runtime behind it, built the way the repository's
/// own water harness (scripts/tests/water) builds one: an uninitialized grain, a real live state
/// and real modules. Every module or system field whose constructor needs only the grain (and
/// fakeable services) is created, so code under test can reach its siblings as it would live.
/// Dependencies are recording fakes; configs are their shipped defaults. The room runs on
/// <see cref="Clock"/>, which stands still until the test advances it.
/// </summary>
public sealed class RoomHarness
{
    public RoomGrain Room { get; }
    internal RoomLiveState State { get; }
    public Fakes Fakes { get; } = new();
    public int Width { get; }
    public int Height { get; }

    /// <summary>
    /// The room's clock (<c>NowMs</c> and <c>UtcNow</c>), from <see cref="ManualTimeProvider.FixedStart"/>
    /// unless the test gave one: room time moves only when the test advances it.
    /// </summary>
    public ManualTimeProvider Clock { get; }

    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public RoomHarness(
        int width = 8,
        int height = 8,
        Altitude[]? heights = null,
        ManualTimeProvider? clock = null
    )
    {
        Width = width;
        Height = height;
        Clock = clock ?? new ManualTimeProvider(ManualTimeProvider.FixedStart);
        Room = (RoomGrain)RuntimeHelpers.GetUninitializedObject(typeof(RoomGrain));

        State = new RoomLiveState { RoomId = 1 };
        Room.UseState(State);
        Room.UseClock(Clock);
        // Players may trade (IPlayerTradePolicy) unless a test says otherwise.
        Fakes.Handlers["MayTradeAsync"] = _ => Task.FromResult(true);

        FillServices();

        var size = width * height;
        State.Model = new RoomModelSnapshot
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
            BaseHeights = heights ?? new Altitude[size],
            BaseFlags = Enumerable.Repeat(RoomTileFlags.Open, size).ToArray(),
        };

        BuildComponents();

        Room.MapModule.EnsureMapBuiltAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public T Module<T>()
        where T : class =>
        (T)typeof(RoomGrain).GetFields(All).First(f => f.FieldType == typeof(T)).GetValue(Room)!;

    public Dictionary<RoomObjectId, IRoomItem> ItemsById => State.ItemsById;

    /// <summary>The room's <c>NowMs</c>: where its monotonic clock stands.</summary>
    public long NowMs() => Room.NowMs();

    /// <summary>The computed height of a tile: the model's floor, or the top of what stands there.</summary>
    public Altitude TileHeight(int x, int y) => State.TileHeights[(y * Width) + x];

    /// <summary>
    /// Creates a floor item with the default floor logic, or the one <paramref name="createLogic"/>
    /// builds from the item's stuff data factory and context.
    /// </summary>
    public RoomFloorItem CreateFloorItem(
        int id,
        int x,
        int y,
        Altitude z,
        Rotation rotation = Rotation.North,
        Altitude? stackHeight = null,
        int width = 1,
        int length = 1,
        bool canStack = true,
        string name = "test_block",
        string logic = "default_floor",
        Func<IStuffDataFactory, IRoomFloorItemContext, IRoomObjectLogic>? createLogic = null,
        FurnitureCategory category = FurnitureCategory.Default,
        string? customParams = null
    )
    {
        var item = new RoomFloorItem
        {
            ObjectId = id,
            OwnerId = 1,
            OwnerName = "test",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 1000 + id,
                SpriteId = 1000 + id,
                Name = name,
                ProductType = ProductType.Floor,
                FurniCategory = category,
                LogicName = logic,
                TotalStates = 2,
                Width = width,
                Length = length,
                StackHeight = stackHeight ?? Altitude.FromInt(100),
                CanStack = canStack,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Everybody,
                ExtraData = null,
                CustomParams = customParams,
            },
        };
        item.SetExtraData(null);
        item.SetPosition(x, y);
        item.SetPositionZ(z);
        item.SetRotation(rotation);
        var context = new RoomFloorItemContext(Room, item);
        var factory = CreateStuffDataFactory();
        item.SetLogic(
            createLogic?.Invoke(factory, context)
                ?? new Turbo.Rooms.Object.Logic.Furniture.Floor.FurnitureFloorLogic(
                    factory,
                    context
                )
        );
        return item;
    }

    private IStuffDataFactory CreateStuffDataFactory()
    {
        var ctor = typeof(StuffDataFactory)
            .GetConstructors(All)
            .OrderBy(c => c.GetParameters().Length)
            .First();
        var args = ctor.GetParameters()
            .Select(p =>
                p.ParameterType.IsGenericType
                && p.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>)
                    ? Activator.CreateInstance(
                        typeof(NullLogger<>).MakeGenericType(
                            p.ParameterType.GetGenericArguments()[0]
                        )
                    )
                : p.ParameterType.IsInterface ? Fakes.Create(p.ParameterType)
                : null
            )
            .ToArray();
        return (IStuffDataFactory)ctor.Invoke(args);
    }

    /// <summary>Registers the item with the room and puts it on the map where it stands.</summary>
    public void AddToRoom(IRoomFloorItem item)
    {
        ItemsById.Add(item.ObjectId, item);
        Room.MapModule.AddItem(item);
    }

    /// <summary>
    /// A player in the room as a fake <see cref="IRoomPlayer"/> whose room object id is its player
    /// id, and whose <c>PlayerId</c> answers that id (as every fake room player's does after).
    /// </summary>
    public IRoomPlayer AddPlayer(int playerId)
    {
        var player = Fakes.Create<IRoomPlayer>(playerId);

        Fakes.Handlers["get_PlayerId"] = call =>
            call.Interface == typeof(IRoomPlayer) && call.Key is int id
                ? (PlayerId)id
                : Fakes.NotHandled;

        State.AvatarsByPlayerId[playerId] = playerId;
        State.AvatarsByObjectId[playerId] = player;

        return player;
    }

    private void FillServices()
    {
        foreach (var f in typeof(RoomGrain).GetFields(All))
        {
            if (f.GetValue(Room) is not null)
                continue;
            var t = f.FieldType;

            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ILogger<>))
            {
                var nl = typeof(NullLogger<>).MakeGenericType(t.GetGenericArguments()[0]);
                f.SetValue(Room, nl.GetField("Instance")!.GetValue(null));
            }
            else if (t == typeof(ILogger))
                f.SetValue(Room, NullLogger.Instance);
            else if (t.Name.EndsWith("Config", StringComparison.Ordinal) && t.IsClass)
                f.SetValue(Room, Activator.CreateInstance(t));
            else if (t.IsInterface && !t.IsGenericType && !t.Name.StartsWith("IAsyncStream"))
                f.SetValue(Room, Fakes.Create(t));
            else if (t.IsInterface && t.IsGenericType && t.Name.StartsWith("IDbContextFactory"))
                f.SetValue(Room, Fakes.Create(t));
        }
    }

    private void BuildComponents()
    {
        // Two passes: some components read siblings in their constructors.
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var f in typeof(RoomGrain).GetFields(All))
            {
                if (f.GetValue(Room) is not null)
                    continue;
                var t = f.FieldType;
                if (!t.IsClass || t.IsAbstract || t.Namespace?.StartsWith("Turbo.Rooms") != true)
                    continue;

                foreach (var ctor in t.GetConstructors(All).OrderBy(c => c.GetParameters().Length))
                {
                    var ps = ctor.GetParameters();
                    if (ps.Length == 0 || ps[0].ParameterType != typeof(RoomGrain))
                        continue;
                    var args = new object?[ps.Length];
                    args[0] = Room;
                    var ok = true;
                    for (var i = 1; i < ps.Length; i++)
                    {
                        var pt = ps[i].ParameterType;
                        if (pt.IsInterface)
                            args[i] = Fakes.Create(pt);
                        else if (
                            pt.IsGenericType
                            && pt.GetGenericTypeDefinition() == typeof(ILogger<>)
                        )
                            args[i] = null;
                        else
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (!ok)
                        continue;
                    try
                    {
                        f.SetValue(Room, ctor.Invoke(args));
                        break;
                    }
                    catch (TargetInvocationException)
                    {
                        // A component that needs the full runtime stays null; tests that need it
                        // set it themselves.
                    }
                }
            }
        }
    }

    public static object? GetField(object target, string name) =>
        target.GetType().GetField(name, All)?.GetValue(target);

    public static void SetField(object target, string name, object? value)
    {
        var f =
            target.GetType().GetField(name, All)
            ?? throw new InvalidOperationException($"No field {name} on {target.GetType()}");
        f.SetValue(target, value);
    }

    public static object? GetMember(object target, string name)
    {
        var p = target.GetType().GetProperty(name, All);
        if (p is not null)
            return p.GetValue(target);
        return target.GetType().GetField(name, All)?.GetValue(target);
    }

    public static void SetMember(object target, string name, object? value)
    {
        var p = target.GetType().GetProperty(name, All);
        if (p is not null)
        {
            var setter = p.GetSetMethod(true);
            if (setter is not null)
            {
                setter.Invoke(target, [value]);
                return;
            }
            var backing = target.GetType().GetField($"<{name}>k__BackingField", All);
            if (backing is not null)
            {
                backing.SetValue(target, value);
                return;
            }
        }
        SetField(target, name, value);
    }
}
