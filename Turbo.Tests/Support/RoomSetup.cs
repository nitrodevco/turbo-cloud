using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Events;
using Turbo.Furniture.Custody;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;

namespace Turbo.Tests.Support;

/// <summary>
/// What a room test sets that the room keeps out of reach. A room grain's dependencies are
/// readonly fields its constructor assigns, and a few things a test scripts (a module's random,
/// the wired clock, an avatar's name) are private or have protected setters, so only reflection
/// can set them. Each member is named here once, by <c>nameof</c> where the test assembly can see
/// it, instead of by a string in each test.
/// </summary>
internal static class RoomSetup
{
    private const BindingFlags ALL =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void UseState(this RoomGrain room, RoomLiveState state) =>
        Replace(room, nameof(RoomGrain._state), state);

    public static void UseEventSystem(this RoomGrain room, EventSystem events) =>
        Replace(room, nameof(RoomGrain._eventSystem), events);

    public static void UseDatabase(this RoomGrain room, IDbContextFactory<TurboDbContext> db) =>
        Replace(room, nameof(RoomGrain._dbCtxFactory), db);

    public static void UseRoomConfig(this RoomGrain room, RoomConfig config) =>
        Replace(room, nameof(RoomGrain._roomConfig), config);

    public static void UseWiredConfig(this RoomGrain room, WiredConfig config) =>
        Replace(room, nameof(RoomGrain._wiredConfig), config);

    public static void UsePetConfig(this RoomGrain room, PetConfig config) =>
        Replace(room, nameof(RoomGrain._petConfig), config);

    public static void UseAvatarProvider(this RoomGrain room, IRoomAvatarProvider avatars) =>
        Replace(room, nameof(RoomGrain._avatarProvider), avatars);

    public static void UseRoomModels(this RoomGrain room, IRoomModelProvider models) =>
        Replace(room, nameof(RoomGrain._roomModelProvider), models);

    public static void UsePetBreeds(this RoomGrain room, IPetBreedProvider breeds) =>
        Replace(room, nameof(RoomGrain._petBreedProvider), breeds);

    public static void UseCommands(this RoomGrain room, ICommandRegistryProvider commands) =>
        Replace(room, nameof(RoomGrain._commandRegistryProvider), commands);

    public static void UseOperatorCommands(this RoomGrain room, IOperatorCommandRunner runner) =>
        Replace(room, nameof(RoomGrain._operatorCommandRunner), runner);

    public static void UseWordFilter(this RoomGrain room, IWordFilter filter) =>
        Replace(room, nameof(RoomGrain._wordFilter), filter);

    public static void UseCustody(this RoomGrain room, IFurnitureCustody custody) =>
        Replace(room, nameof(RoomGrain._custody), custody);

    public static void UseLogger(this RoomGrain room, ILogger<IRoomGrain> logger) =>
        Replace(room, nameof(RoomGrain._logger), logger);

    /// <summary>The clock the room's <c>NowMs</c> and <c>UtcNow</c> read.</summary>
    public static void UseClock(this RoomGrain room, TimeProvider time) =>
        Replace(room, nameof(RoomGrain._time), time);

    /// <summary>Marks the room's tick as running (or not), as the tick timer does around a tick.</summary>
    public static void SetTickRunning(this RoomGrain room, bool running) =>
        Replace(room, "_tickRunning", running);

    /// <summary>Gives the live state another room id (init-only: the grain's constructor sets it).</summary>
    public static void UseRoomId(this RoomLiveState state, RoomId roomId) =>
        SetProperty(state, nameof(RoomLiveState.RoomId), roomId);

    /// <summary>The security module's database (its own copy of the grain's).</summary>
    public static void UseDatabase(
        this RoomSecurityModule security,
        IDbContextFactory<TurboDbContext> db
    ) => Replace(security, "_dbCtxFactory", db);

    /// <summary>The random a pet module draws from, for a test that scripts what it rolls.</summary>
    public static void UseRandom(this RoomPetModule pets, Random random) =>
        Replace(pets, "_random", random);

    /// <summary>The wall clock the wired system reads, in milliseconds.</summary>
    public static void UseWallClock(this RoomWiredSystem wired, Func<long> nowMs) =>
        Replace(wired, "_wallClockMs", nowMs);

    /// <summary>The wired system's registered variables, by id.</summary>
    public static Dictionary<WiredVariableId, IWiredVariable> VariablesById(
        this RoomWiredSystem wired
    ) => (Dictionary<WiredVariableId, IWiredVariable>)Read(wired, "_variableById");

    /// <summary>The room's armed timers, by the furni that set them.</summary>
    public static IDictionary TimersByObjectId(this RoomTimerSystem timers) =>
        (IDictionary)Read(timers, "_timersByObjectId");

    /// <summary>Names an avatar; the room names avatars from their snapshots, so the setter is protected.</summary>
    public static void UseName(this IRoomAvatar avatar, string name) =>
        SetProperty(avatar, nameof(IRoomAvatar.Name), name);

    /// <summary>Gives a placed item another definition (init-only on the item).</summary>
    public static void UseDefinition(this IRoomItem item, FurnitureDefinitionSnapshot definition) =>
        SetProperty(item, nameof(IRoomItem.Definition), definition);

    /// <summary>Puts an object at a height without the side effects of <c>SetPositionZ</c>.</summary>
    public static void UseZ(this IRoomObject roomObject, Altitude z) =>
        SetProperty(roomObject, nameof(IRoomObject.Z), z);

    private static object Read(object target, string field) =>
        Field(target.GetType(), field).GetValue(target)!;

    private static void Replace(object target, string field, object? value) =>
        Field(target.GetType(), field).SetValue(target, value);

    private static FieldInfo Field(Type type, string field) =>
        type.GetField(field, ALL)
        ?? throw new InvalidOperationException($"{type.Name} has no field {field}.");

    private static void SetProperty(object target, string property, object? value) =>
        (
            target.GetType().GetProperty(property, ALL)
            ?? throw new InvalidOperationException(
                $"{target.GetType().Name} has no property {property}."
            )
        ).SetValue(target, value);
}
