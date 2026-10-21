using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Context;
using Turbo.Events;

namespace Turbo.Tests.Support;

/// <summary>
/// Builds any Turbo grain without an Orleans runtime: an uninitialized instance, its
/// &lt;Grain&gt;LiveState created with the given key, dependencies filled by type (the given
/// database, the furniture custody over it, shipped config defaults, null loggers, a recording grain factory, an event system
/// with no handlers), and a fake
/// grain context so timer registration is inert.
/// </summary>
public static class GrainHarness
{
    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary><see cref="Create(Assembly, string, Fakes, IDbContextFactory{TurboDbContext}?, int)"/> for a grain type the test can name.</summary>
    public static TGrain Create<TGrain>(
        Fakes fakes,
        IDbContextFactory<TurboDbContext>? db = null,
        int playerId = 1
    )
        where TGrain : Grain =>
        (TGrain)Create(typeof(TGrain).Assembly, typeof(TGrain).FullName!, fakes, db, playerId);

    /// <summary>
    /// Puts <paramref name="value"/> in the one field of <paramref name="grain"/> declared as
    /// <typeparamref name="T"/>: a dependency (a clock, a config, a module) or the grain's live
    /// state. Grains keep these in private readonly fields, so the field is found by its type, not
    /// its name.
    /// </summary>
    public static void Replace<T>(object grain, T value) =>
        FieldOf<T>(grain).SetValue(grain, value);

    /// <summary>The value of the one field of <paramref name="grain"/> declared as <typeparamref name="T"/>, such as its live state.</summary>
    public static T Get<T>(object grain) => (T)FieldOf<T>(grain).GetValue(grain)!;

    private static FieldInfo FieldOf<T>(object grain)
    {
        var fields = grain.GetType().GetFields(All).Where(f => f.FieldType == typeof(T)).ToList();

        return fields.Count == 1
            ? fields[0]
            : throw new InvalidOperationException(
                $"{grain.GetType().Name} has {fields.Count} fields of type {typeof(T).Name}."
            );
    }

    public static object Create(
        Assembly assembly,
        string typeName,
        Fakes fakes,
        IDbContextFactory<TurboDbContext>? db = null,
        int playerId = 1
    )
    {
        var type = assembly.GetType(typeName) ?? throw new InvalidOperationException(typeName);
        var grain = RuntimeHelpers.GetUninitializedObject(type);

        var stateField = type.GetFields(All).FirstOrDefault(f => f.Name == "_state");
        if (stateField is not null)
        {
            var state = Activator.CreateInstance(stateField.FieldType, nonPublic: true)!;
            var pid = stateField.FieldType.GetProperty("PlayerId", All);
            if (pid is not null)
                RoomHarness.SetMember(
                    state,
                    "PlayerId",
                    (Turbo.Primitives.Players.PlayerId)playerId
                );
            stateField.SetValue(grain, state);
        }

        db ??= new InMemoryDb();

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
            else if (t == typeof(IDbContextFactory<TurboDbContext>))
                f.SetValue(grain, db);
            else if (t == typeof(Turbo.Furniture.Custody.IFurnitureCustody))
                f.SetValue(grain, TestCustody.Over(db));
            else if (t.Name.EndsWith("Config", StringComparison.Ordinal) && t.IsClass)
                f.SetValue(grain, Activator.CreateInstance(t));
            else if (t == typeof(IGrainFactory))
                f.SetValue(grain, fakes.Create<IGrainFactory>());
            else if (t == typeof(Turbo.Database.Achievements.IAchievementFactRecorder))
                f.SetValue(
                    grain,
                    fakes.Create<Turbo.Database.Achievements.IAchievementFactRecorder>()
                );
            else if (t == typeof(Turbo.Database.Movements.IAssetMovementRecorder))
                f.SetValue(grain, fakes.Create<Turbo.Database.Movements.IAssetMovementRecorder>());
            else if (t == typeof(TimeProvider))
                f.SetValue(grain, TimeProvider.System);
            else if (t == typeof(EventSystem))
                f.SetValue(grain, new TestEventBus().System);
            else if (t == typeof(Turbo.Primitives.Moderation.IWordFilter))
                f.SetValue(grain, new PassThroughWordFilter());
        }

        fakes.Handlers.TryAdd(
            "GetService",
            call => call.Args[0] is Type st && st.IsInterface ? fakes.Create(st) : null
        );
        var ctx = typeof(Grain).GetProperty("GrainContext", All)!.GetSetMethod(true);
        ctx?.Invoke(grain, [fakes.Create<IGrainContext>(typeName)]);
        typeof(Grain)
            .GetField("<Runtime>k__BackingField", All)
            ?.SetValue(grain, fakes.Create<IGrainRuntime>(typeName));
        return grain;
    }
}
