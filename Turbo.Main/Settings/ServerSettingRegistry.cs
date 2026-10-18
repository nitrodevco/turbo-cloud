using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Settings;

namespace Turbo.Main.Settings;

/// <summary>
/// Every setting the host has: each option of each config class a module registers
/// (<c>services.Configure&lt;T&gt;(section)</c>) that names its section in a <c>SECTION_NAME</c>
/// constant, down to its values. A nested class is a section of its own settings; a list or map
/// is one setting, taken whole.
/// </summary>
internal sealed class ServerSettingRegistry(ImmutableArray<ServerSettingRegistry.Section> sections)
{
    private const string SECTION_NAME = "SECTION_NAME";

    // Deeper than any config nests: a class that holds itself would never end.
    private const int MAX_DEPTH = 8;

    public ImmutableArray<Section> Sections { get; } = sections;

    public static ServerSettingRegistry From(IServiceCollection services) =>
        new([
            .. services
                .Where(x =>
                    x.ServiceType.IsGenericType
                    && x.ServiceType.GetGenericTypeDefinition()
                        == typeof(IOptionsChangeTokenSource<>)
                )
                .Select(x => x.ServiceType.GetGenericArguments()[0])
                .Distinct()
                .Select(type => (Type: type, Name: SectionOf(type)))
                .Where(x => x.Name is not null)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Type.FullName, StringComparer.Ordinal)
                .Select(x => Build(x.Type, x.Name!, null, SettingDocs.Summary)),
        ]);

    /// <summary>
    /// The section of a plugin's config class, its summaries read with <paramref name="summary"/>;
    /// null for a class that names no section.
    /// </summary>
    public static Section? PluginSection(
        Type type,
        string plugin,
        Func<PropertyInfo, string> summary
    ) => SectionOf(type) is { } name ? Build(type, name, plugin, summary) : null;

    private static Section Build(
        Type type,
        string name,
        string? plugin,
        Func<PropertyInfo, string> summary
    ) =>
        new(
            name,
            type,
            [
                .. Walk(
                    type,
                    name,
                    [],
                    type.IsDefined(typeof(StartupSettingAttribute)),
                    // A plugin reads its settings through IOptionsMonitor: a change applies at once.
                    plugin is not null
                        || type.IsDefined(typeof(LiveSettingAttribute)),
                    summary
                ),
            ],
            plugin
        );

    private static string? SectionOf(Type type) =>
        type.GetField(SECTION_NAME, BindingFlags.Public | BindingFlags.Static)
            is { IsLiteral: true } field
            ? field.GetRawConstantValue() as string
            : null;

    private static IEnumerable<Setting> Walk(
        Type type,
        string path,
        ImmutableArray<PropertyInfo> chain,
        bool startup,
        bool live,
        Func<PropertyInfo, string> summary
    )
    {
        foreach (
            var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x =>
                    x.GetIndexParameters().Length == 0 && x.CanRead && x.SetMethod?.IsPublic == true
                )
        )
        {
            var at = chain.Add(property);
            var name = $"{path}{SettingJson.KEY_DELIMITER}{property.Name}";
            var isStartup = startup || property.IsDefined(typeof(StartupSettingAttribute));
            var isLive = live || property.IsDefined(typeof(LiveSettingAttribute));
            var kind = KindOf(property.PropertyType);

            if (kind is null)
            {
                if (at.Length >= MAX_DEPTH)
                    continue;

                foreach (
                    var nested in Walk(
                        property.PropertyType,
                        name,
                        at,
                        isStartup
                            || property.PropertyType.IsDefined(typeof(StartupSettingAttribute)),
                        isLive || property.PropertyType.IsDefined(typeof(LiveSettingAttribute)),
                        summary
                    )
                )
                    yield return nested;

                continue;
            }

            var value = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            yield return new Setting(
                name,
                property.PropertyType,
                at,
                kind,
                value.IsEnum ? [.. Enum.GetNames(value)] : [],
                property.IsDefined(typeof(SecretSettingAttribute)),
                isStartup,
                isLive,
                summary(property),
                kind == "string" && property.IsDefined(typeof(UrlSettingAttribute))
            );
        }
    }

    /// <summary>What a value of the type is to the panel; null for a class whose properties are settings of their own.</summary>
    private static string? KindOf(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string) || type == typeof(Uri))
            return "string";

        if (type == typeof(bool))
            return "bool";

        if (type.IsEnum)
            return "enum";

        if (type == typeof(TimeSpan))
            return "duration";

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return "number";

        if (type.IsPrimitive)
            return "integer";

        if (
            type.GetInterfaces()
                .Append(type)
                .Any(x =>
                    x.IsGenericType
                    && (
                        x.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                        || x.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)
                    )
                )
        )
            return "map";

        if (typeof(IEnumerable).IsAssignableFrom(type))
            return "list";

        return type.IsClass && !type.IsAbstract ? null : "json";
    }

    /// <summary>A config class and the section it is bound to; <paramref name="Plugin"/> is the key of the plugin it is from.</summary>
    public sealed record Section(
        string Name,
        Type Type,
        ImmutableArray<Setting> Settings,
        string? Plugin = null
    );

    /// <summary>One setting: its path, and the properties that lead to it from its section's class.</summary>
    public sealed record Setting(
        string Path,
        Type Type,
        ImmutableArray<PropertyInfo> Chain,
        string Kind,
        ImmutableArray<string> Options,
        bool Secret,
        bool Startup,
        bool Live,
        string Summary,
        bool Url = false
    )
    {
        /// <summary>The setting's value in an instance of its section's class; null along a null class.</summary>
        public object? ReadFrom(object? section)
        {
            foreach (var property in Chain)
            {
                if (section is null)
                    return null;

                section = property.GetValue(section);
            }

            return section;
        }
    }
}
