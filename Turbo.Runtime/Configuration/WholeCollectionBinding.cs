using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Turbo.Runtime.Configuration;

/// <summary>
/// Makes a list, set or map the configuration gives the whole of it. The configuration binder adds
/// what it reads to the items a config class starts with, so <c>[10, 20]</c> over a class's
/// <c>[70, 20, 7, 3]</c> came out as <c>[70, 20, 7, 3, 10, 20]</c> and a reader of the first items
/// never saw the change. After the binder, each collection the configuration names (an empty one
/// included) is built again from the configuration alone; one it doesn't name keeps the class's
/// items. The section is the class's <c>SECTION_NAME</c> constant, as the admin panel reads it.
/// </summary>
public static class WholeCollectionBinding
{
    private const string SECTION_NAME = "SECTION_NAME";

    // Deeper than any config nests: a class that holds itself would never end.
    private const int MAX_DEPTH = 8;

    /// <summary>
    /// Binds the collections of every config class of the container whole. The rebind goes
    /// first among the steps after binding, wherever it is added: a module's own step after
    /// binding (Habbo's Habbicons for a hotel that lists none) builds on the configured lists,
    /// and is not undone by them.
    /// </summary>
    public static IServiceCollection AddWholeCollectionBinding(this IServiceCollection services)
    {
        var descriptor = ServiceDescriptor.Singleton(
            typeof(IPostConfigureOptions<>),
            typeof(WholeCollectionPostConfigure<>)
        );

        if (
            !services.Any(x =>
                x.ServiceType == descriptor.ServiceType
                && x.ImplementationType == descriptor.ImplementationType
            )
        )
            services.Insert(0, descriptor);

        return services;
    }

    /// <summary>The section a config class names in its <c>SECTION_NAME</c> constant; null for none.</summary>
    public static string? SectionOf(Type type) =>
        type.GetField(SECTION_NAME, BindingFlags.Public | BindingFlags.Static)
            is { IsLiteral: true } field
            ? field.GetRawConstantValue() as string
            : null;

    /// <summary>
    /// A new instance of <paramref name="type"/> bound to the section at <paramref name="path"/> as
    /// its options are: the binder's way, then each collection whole.
    /// </summary>
    public static object Get(IConfiguration root, string path, Type type)
    {
        var section = root.GetSection(path);
        var value = section.Get(type) ?? Activator.CreateInstance(type)!;

        Rebind(value, root, section);

        return value;
    }

    /// <inheritdoc cref="Get(IConfiguration, string, Type)"/>
    public static T Get<T>(IConfiguration root, string path)
        where T : class => (T)Get(root, path, typeof(T));

    /// <summary>
    /// Builds again, from <paramref name="section"/> alone, each collection of
    /// <paramref name="target"/> (and of the classes it holds) the section names.
    /// </summary>
    public static void Rebind(object target, IConfiguration root, IConfigurationSection section) =>
        Rebind(target, root, section, 0);

    private static void Rebind(
        object target,
        IConfiguration root,
        IConfigurationSection section,
        int depth
    )
    {
        if (depth >= MAX_DEPTH)
            return;

        foreach (
            var property in target
                .GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x =>
                    x.GetIndexParameters().Length == 0 && x.CanRead && x.SetMethod?.IsPublic == true
                )
        )
        {
            var child = section.GetSection(property.Name);

            if (IsCollection(property.PropertyType))
            {
                if (Names(root, child))
                    property.SetValue(target, Build(property.PropertyType, root, child, depth));

                continue;
            }

            if (
                IsNested(property.PropertyType)
                && child.GetChildren().Any()
                && property.GetValue(target) is { } nested
            )
                Rebind(nested, root, child, depth + 1);
        }
    }

    /// <summary>
    /// Whether the configuration gives the collection at the section: its items, or none, as a
    /// JSON file gives an empty list (its key, with no value). Text where a list goes is left to
    /// the binder, which ignores it.
    /// </summary>
    private static bool Names(IConfiguration root, IConfigurationSection section) =>
        section.GetChildren().Any()
        || section.Value is ""
        || (
            section.Value is null
            && root is IConfigurationRoot configurationRoot
            && configurationRoot.Providers.Any(x => x.TryGet(section.Path, out _))
        );

    private static object? Build(
        Type type,
        IConfiguration root,
        IConfigurationSection section,
        int depth
    )
    {
        // Into a collection of its own making the binder has nothing to add to.
        var value = section.GetChildren().Any() ? section.Get(type) : null;

        value ??= Empty(type);

        // Items that are classes of their own were bound over their defaults just the same.
        if (value is IDictionary map)
        {
            foreach (var child in section.GetChildren())
            foreach (DictionaryEntry entry in map)
                if (
                    string.Equals(
                        Convert.ToString(entry.Key, CultureInfo.InvariantCulture),
                        child.Key,
                        StringComparison.OrdinalIgnoreCase
                    )
                    && entry.Value is { } item
                    && IsNested(item.GetType())
                )
                    Rebind(item, root, child, depth + 1);
        }
        else if (value is IList list && ElementOf(type) is { } element && IsNested(element))
        {
            var children = section.GetChildren().ToList();

            for (var i = 0; i < list.Count && i < children.Count; i++)
                if (list[i] is { } item)
                    Rebind(item, root, children[i], depth + 1);
        }

        return value;
    }

    private static object? Empty(Type type)
    {
        if (type.IsArray)
            return Array.CreateInstance(type.GetElementType()!, 0);

        if (!type.IsInterface)
            return type.IsAbstract ? null : Activator.CreateInstance(type);

        if (Generic(type, typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>)) is { } map)
            return Activator.CreateInstance(
                typeof(Dictionary<,>).MakeGenericType(map.GetGenericArguments())
            );

        if (Generic(type, typeof(ISet<>), typeof(IReadOnlySet<>)) is { } set)
            return Activator.CreateInstance(
                typeof(HashSet<>).MakeGenericType(set.GetGenericArguments())
            );

        return ElementOf(type) is { } element
            ? Activator.CreateInstance(typeof(List<>).MakeGenericType(element))
            : null;
    }

    private static Type? ElementOf(Type type) =>
        type.IsArray
            ? type.GetElementType()
            : Generic(type, typeof(IEnumerable<>))?.GetGenericArguments()[0];

    private static Type? Generic(Type type, params Type[] definitions) =>
        type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(x =>
                x.IsGenericType && definitions.Contains(x.GetGenericTypeDefinition())
            );

    private static bool IsCollection(Type type) =>
        type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    private static bool IsNested(Type type) =>
        type.IsClass
        && !type.IsAbstract
        && type != typeof(string)
        && type != typeof(Uri)
        && !typeof(IEnumerable).IsAssignableFrom(type);

    /// <summary>Rebinds the options of a class that names its section, after the binder.</summary>
    private sealed class WholeCollectionPostConfigure<T>(IConfiguration configuration)
        : IPostConfigureOptions<T>
        where T : class
    {
        private static readonly string? Section = SectionOf(typeof(T));

        public void PostConfigure(string? name, T options)
        {
            if (Section is null || (name ?? Options.DefaultName) != Options.DefaultName)
                return;

            Rebind(options, configuration, configuration.GetSection(Section));
        }
    }
}
