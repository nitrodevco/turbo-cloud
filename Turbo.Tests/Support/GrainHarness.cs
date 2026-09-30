using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Context;

namespace Turbo.Tests.Support;

/// <summary>
/// Builds any Turbo grain without an Orleans runtime: an uninitialized instance, its
/// &lt;Grain&gt;LiveState created with the given key, dependencies filled by type (the given
/// database, shipped config defaults, null loggers, a recording grain factory), and a fake
/// grain context so timer registration is inert.
/// </summary>
public static class GrainHarness
{
    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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
                f.SetValue(grain, db ?? new InMemoryDb());
            else if (t.Name.EndsWith("Config", StringComparison.Ordinal) && t.IsClass)
                f.SetValue(grain, Activator.CreateInstance(t));
            else if (t == typeof(IGrainFactory))
                f.SetValue(grain, fakes.Create<IGrainFactory>());
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
