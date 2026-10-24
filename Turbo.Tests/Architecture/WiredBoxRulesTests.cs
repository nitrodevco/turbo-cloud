using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Architecture;

/// <summary>
/// What every wired box logic must hold, one fact per rule listing every box that breaks it: a
/// box that is not registered is never built, two boxes with one (type, code) open the same
/// editor in the client, and a box whose rules refuse its own defaults cannot be saved as it
/// opened (the editor sends them back as they came; a rule may still pull one into its bounds,
/// as Habbo stores Match To Snapshot's fifth int 1 as 0).
/// </summary>
public sealed class WiredBoxRulesTests
{
    private static readonly MethodInfo NORMALIZE = typeof(FurnitureWiredLogic).GetMethod(
        "TryNormalizeIntParams",
        BindingFlags.Instance | BindingFlags.NonPublic
    )!;

    [Fact]
    public void Every_wired_box_is_registered_under_one_wf_name()
    {
        var concrete = typeof(FurnitureWiredLogic)
            .Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(FurnitureWiredLogic)));
        var keys = Boxes().GroupBy(t => Key(t)).Where(g => g.Count() > 1);
        var offenders = concrete
            .Where(t => t.GetCustomAttribute<RoomObjectLogicAttribute>(false) is null)
            .Select(t => $"{t.Name} has no [RoomObjectLogic]")
            .Concat(
                Boxes()
                    .Where(t => !Key(t).StartsWith("wf_", StringComparison.Ordinal))
                    .Select(t => $"{t.Name} is registered as {Key(t)}, not a wf_ name")
            )
            .Concat(
                keys.Select(g =>
                    $"{g.Key} is registered by {string.Join(", ", g.Select(t => t.Name))}"
                )
            );

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Every_wired_box_has_a_type_and_a_code_of_its_own()
    {
        var room = new WiredRoom();
        var built = Boxes().Select((t, i) => Build(room, t, i + 1)).ToList();
        var offenders = built
            .Where(b => b.WiredType == WiredType.None || b.WiredCode < 0)
            .Select(b => $"{b.GetType().Name} is {b.WiredType} {b.WiredCode}")
            .Concat(
                built
                    .Where(b => !IsAlias(b.GetType()))
                    .GroupBy(b => (b.WiredType, b.WiredCode))
                    .Where(g => g.Count() > 1)
                    .Select(g =>
                        $"{g.Key.WiredType} {g.Key.WiredCode} is the code of {string.Join(", ", g.Select(b => b.GetType().Name))}"
                    )
            );

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Every_wired_box_accepts_its_own_defaults()
    {
        var room = new WiredRoom();
        var offenders = new List<string>();

        foreach (var (type, i) in Boxes().Select((t, i) => (t, i + 1)))
        {
            var box = Build(room, type, i);
            var defaults = box.GetDefaultIntParams();
            object?[] args = [defaults.ToList(), null];

            if (!(bool)NORMALIZE.Invoke(box, args)!)
                offenders.Add($"{type.Name} refuses its defaults [{string.Join(", ", defaults)}]");
        }

        offenders.Should().BeEmpty();
    }

    private static IEnumerable<Type> Boxes() =>
        typeof(FurnitureWiredLogic)
            .Assembly.GetTypes()
            .Where(t =>
                !t.IsAbstract
                && t.IsSubclassOf(typeof(FurnitureWiredLogic))
                && t.GetCustomAttribute<RoomObjectLogicAttribute>(false) is not null
            )
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    // A second furni of a box, built as its subclass without a code of its own
    // (wf_trg_at_time_long is the long periodic trigger again).
    private static bool IsAlias(Type type) =>
        type.BaseType is { IsAbstract: false } parent
        && parent.GetCustomAttribute<RoomObjectLogicAttribute>(false) is not null
        && type.GetProperty(nameof(FurnitureWiredLogic.WiredCode))!.DeclaringType != type;

    private static string Key(Type type) =>
        type.GetCustomAttribute<RoomObjectLogicAttribute>(false)!.Key;

    private static FurnitureWiredLogic Build(WiredRoom room, Type type, int id)
    {
        FurnitureWiredLogic? box = null;

        room.AddFloorItem(
            id,
            1,
            1,
            Key(type),
            createLogic: (factory, ctx) =>
                box = (FurnitureWiredLogic)
                    ActivatorUtilities.CreateInstance(
                        room.Harness.Services,
                        type,
                        room.Grains,
                        factory,
                        ctx
                    )
        );

        return box!;
    }
}
