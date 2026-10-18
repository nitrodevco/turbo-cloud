using System.Collections;
using System.Globalization;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Rooms.Configuration;
using Turbo.Runtime.Configuration;
using Xunit;

namespace Turbo.Tests.Settings;

/// <summary>
/// A list, set or map the configuration gives is the whole of it, in every config class the
/// server has: never added to the items the class starts with (the binder's way, which kept
/// <c>Turbo:Pets:BreedingRarityChances</c> at its defaults whatever was configured). One the
/// configuration doesn't give keeps the class's items.
/// </summary>
public sealed class WholeCollectionBindingTests
{
    private const int MAX_DEPTH = 6;

    public static TheoryData<string> ConfigClasses() => [.. Classes().Select(x => x.FullName!)];

    [Theory]
    [MemberData(nameof(ConfigClasses))]
    public void every_collection_configured_with_one_item_binds_to_exactly_that_item(
        string className
    )
    {
        var type = Classes().Single(x => x.FullName == className);
        var section = WholeCollectionBinding.SectionOf(type)!;
        var keys = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var expected = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        Fill(type, section, keys, expected, 0);

        var bound = Bind(type, keys);
        var failures = new List<string>();

        Check(bound, section, expected, failures, 0);

        string.Join(Environment.NewLine, failures).Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(ConfigClasses))]
    public void every_collection_left_unconfigured_keeps_the_items_its_class_starts_with(
        string className
    )
    {
        var type = Classes().Single(x => x.FullName == className);
        var section = WholeCollectionBinding.SectionOf(type)!;
        var bound = Bind(type, new Dictionary<string, string?> { [$"{section}:Unrelated"] = "1" });
        var failures = new List<string>();

        Same(Activator.CreateInstance(type)!, bound, section, failures, 0);

        string.Join(Environment.NewLine, failures).Should().BeEmpty();
    }

    [Fact]
    public void the_breeding_rarity_chances_configured_are_the_chances_not_the_defaults_and_them()
    {
        var bound = (PetConfig)Bind(
            typeof(PetConfig),
            new Dictionary<string, string?>
            {
                ["Turbo:Pets:BreedingRarityChances:0"] = "40",
                ["Turbo:Pets:BreedingRarityChances:1"] = "30",
                ["Turbo:Pets:BreedingRarityChances:2"] = "20",
                ["Turbo:Pets:BreedingRarityChances:3"] = "10",
            }
        );

        bound.BreedingRarityChances.Should().Equal(40, 30, 20, 10);
    }

    [Fact]
    public void a_list_a_file_empties_is_empty_and_one_it_never_names_keeps_its_defaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(
                new MemoryStream(
                    """{ "Turbo": { "Pets": { "BreedingRarityChances": [] } } }"""u8.ToArray()
                )
            )
            .Build();

        Bind<PetConfig>(configuration).BreedingRarityChances.Should().BeEmpty();
        Bind<RoomConfig>(configuration)
            .GameTeamEffectIds.Should()
            .Equal(new RoomConfig().GameTeamEffectIds);
    }

    [Fact]
    public void a_live_reader_sees_the_new_list_whole_after_the_configuration_changes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Turbo:Pets:BreedingRarityChances:0"] = "1" }
            )
            .Build();
        using var services = Services<PetConfig>(configuration);
        var monitor = services.GetRequiredService<IOptionsMonitor<PetConfig>>();

        monitor.CurrentValue.BreedingRarityChances.Should().Equal(1);

        configuration["Turbo:Pets:BreedingRarityChances:0"] = "5";
        configuration["Turbo:Pets:BreedingRarityChances:1"] = "6";
        configuration.Reload();

        monitor.CurrentValue.BreedingRarityChances.Should().Equal(5, 6);
    }

    private static object Bind(Type type, Dictionary<string, string?> keys)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(keys).Build();
        var bind = typeof(WholeCollectionBindingTests)
            .GetMethod(
                nameof(Bind),
                BindingFlags.NonPublic | BindingFlags.Static,
                [typeof(IConfiguration)]
            )!
            .MakeGenericMethod(type);

        try
        {
            return bind.Invoke(null, [configuration])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static T Bind<T>(IConfiguration configuration)
        where T : class
    {
        using var services = Services<T>(configuration);

        return services.GetRequiredService<IOptions<T>>().Value;
    }

    // As a module registers its class, in a host that binds collections whole.
    private static ServiceProvider Services<T>(IConfiguration configuration)
        where T : class
    {
        var services = new ServiceCollection();

        services.AddSingleton(configuration);
        services.AddWholeCollectionBinding();
        services.Configure<T>(
            configuration.GetSection(WholeCollectionBinding.SectionOf(typeof(T))!)
        );

        return services.BuildServiceProvider();
    }

    /// <summary>Every config class of the server's assemblies that names its section.</summary>
    private static IEnumerable<Type> Classes()
    {
        var assemblies = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var queue = new Queue<Assembly>([typeof(WholeCollectionBindingTests).Assembly]);

        while (queue.TryDequeue(out var assembly))
            foreach (var name in assembly.GetReferencedAssemblies())
                if (
                    name.Name is { } simple
                    && simple.StartsWith("Turbo.", StringComparison.Ordinal)
                    && !assemblies.ContainsKey(simple)
                )
                {
                    var loaded = Assembly.Load(name);

                    assemblies[simple] = loaded;
                    queue.Enqueue(loaded);
                }

        return assemblies
            .Values.SelectMany(x => x.GetTypes())
            .Where(x =>
                x.IsClass
                && !x.IsAbstract
                && !x.IsGenericTypeDefinition
                && x.GetConstructor(Type.EmptyTypes) is not null
                && WholeCollectionBinding.SectionOf(x) is not null
            )
            .OrderBy(x => x.FullName, StringComparer.Ordinal);
    }

    /// <summary>Configures each collection under <paramref name="path"/> with one item.</summary>
    private static void Fill(
        Type type,
        string path,
        Dictionary<string, string?> keys,
        Dictionary<string, string?> expected,
        int depth
    )
    {
        foreach (var property in Settable(type))
        {
            var at = $"{path}:{property.Name}";

            if (IsCollection(property.PropertyType))
            {
                var before = keys.Count;
                var item = One(property.PropertyType, at, keys, depth);

                // A collection of what no key can say isn't configurable at all.
                if (keys.Count > before)
                    expected[at] = item;
            }
            else if (IsNested(property.PropertyType) && depth < MAX_DEPTH)
                Fill(property.PropertyType, at, keys, expected, depth + 1);
        }
    }

    /// <summary>One item of the collection, at <paramref name="at"/>; its text when it is a value.</summary>
    private static string? One(
        Type collection,
        string at,
        Dictionary<string, string?> keys,
        int depth
    )
    {
        if (Map(collection) is { } map)
            return KeyOf(map[0]) is { } key ? Value(map[1], $"{at}:{key}", keys, depth) : null;

        return Element(collection) is { } element ? Value(element, $"{at}:0", keys, depth) : null;
    }

    private static string? Value(Type type, string at, Dictionary<string, string?> keys, int depth)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (ScalarOf(type) is { } scalar)
        {
            keys[at] = scalar;

            return scalar;
        }

        if (IsCollection(type))
        {
            One(type, at, keys, depth + 1);

            return null;
        }

        if (IsNested(type) && depth < MAX_DEPTH)
            foreach (var property in Settable(type))
                if (
                    ScalarOf(
                        Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType
                    ) is
                    { } first
                )
                {
                    keys[$"{at}:{property.Name}"] = first;

                    break;
                }

        return null;
    }

    private static void Check(
        object target,
        string path,
        Dictionary<string, string?> expected,
        List<string> failures,
        int depth
    )
    {
        foreach (var property in Settable(target.GetType()))
        {
            var at = $"{path}:{property.Name}";
            var value = property.GetValue(target);

            if (expected.TryGetValue(at, out var item))
            {
                var items = Items(value);

                if (items.Count != 1)
                    failures.Add($"{at} has {items.Count} items, configured with 1");
                else if (
                    item is not null
                    && !string.Equals(Text(items[0]), item, StringComparison.OrdinalIgnoreCase)
                )
                    failures.Add($"{at} has {Text(items[0])}, configured with {item}");
            }
            else if (value is not null && IsNested(property.PropertyType) && depth < MAX_DEPTH)
                Check(value, at, expected, failures, depth + 1);
        }
    }

    private static void Same(
        object defaults,
        object bound,
        string path,
        List<string> failures,
        int depth
    )
    {
        foreach (var property in Settable(defaults.GetType()))
        {
            var at = $"{path}:{property.Name}";
            var start = property.GetValue(defaults);
            var now = property.GetValue(bound);

            if (IsCollection(property.PropertyType))
            {
                var was = Items(start).Select(Text).ToList();
                var got = Items(now).Select(Text).ToList();

                if (!was.SequenceEqual(got))
                    failures.Add(
                        $"{at} is [{string.Join(", ", got)}], its class starts it with [{string.Join(", ", was)}]"
                    );
            }
            else if (
                start is not null
                && now is not null
                && IsNested(property.PropertyType)
                && depth < MAX_DEPTH
            )
                Same(start, now, at, failures, depth + 1);
        }
    }

    private static List<object?> Items(object? value) =>
        value switch
        {
            null => [],
            IDictionary map => [.. map.Values.Cast<object?>()],
            IEnumerable items => [.. items.Cast<object?>()],
            _ => [],
        };

    private static string? Text(object? value) =>
        value switch
        {
            null => null,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ when value.GetType().IsClass && value is not string => value.GetType().Name,
            _ => value.ToString(),
        };

    private static IEnumerable<PropertyInfo> Settable(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x =>
                x.GetIndexParameters().Length == 0 && x.CanRead && x.SetMethod?.IsPublic == true
            );

    private static string? ScalarOf(Type type)
    {
        if (type == typeof(string))
            return "one";

        if (type == typeof(bool))
            return "True";

        if (type.IsEnum)
            return Enum.GetNames(type).Last();

        if (type == typeof(TimeSpan))
            return "00:00:07";

        if (type == typeof(Uri))
            return "https://one.example/";

        if (type == typeof(char))
            return "a";

        if (type.IsPrimitive || type == typeof(decimal))
            return "7";

        return null;
    }

    private static string? KeyOf(Type type) =>
        type == typeof(string) || type.IsEnum || (type.IsPrimitive && type != typeof(bool))
            ? ScalarOf(type)
            : null;

    private static Type[]? Map(Type type) =>
        type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(x =>
                x.IsGenericType
                && (
                    x.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                    || x.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)
                )
            )
            ?.GetGenericArguments();

    private static Type? Element(Type type) =>
        type.IsArray
            ? type.GetElementType()
            : type.GetInterfaces()
                .Append(type)
                .FirstOrDefault(x =>
                    x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                )
                ?.GetGenericArguments()[0];

    private static bool IsCollection(Type type) =>
        type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    private static bool IsNested(Type type) =>
        type.IsClass
        && !type.IsAbstract
        && type != typeof(string)
        && type != typeof(Uri)
        && !IsCollection(type);
}
