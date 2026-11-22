using System.Reflection;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Main.Settings;
using Turbo.Runtime.Configuration;
using Xunit;

namespace Turbo.Tests.Settings;

/// <summary>
/// Every key the shipped settings files set under <c>Turbo</c> is an option the server reads: a
/// setting the admin panel lists (<see cref="ServerSettingRegistry"/>), or an entry of a list or
/// map setting. The binder ignores a key that names nothing, so a hotel that edits one changes
/// nothing and is told nothing. <c>Turbo:Rooms:TrophyInscriptionMaxLength</c> stayed in
/// appsettings.json after the option moved to <c>Turbo:Catalog</c>.
/// </summary>
public sealed class ShippedAppSettingsTests
{
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    [InlineData("appsettings.Production.json")]
    public void every_key_under_Turbo_names_an_option(string file)
    {
        var settings = ServerSettingRegistry
            .From(ConfigClasses())
            .Sections.SelectMany(x => x.Settings)
            .ToList();
        var root = JsonNode.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", file)),
            documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip }
        )!;

        Keys(root["Turbo"], "Turbo")
            .Where(path => !Names(settings, path))
            .ToList()
            .Should()
            .BeEmpty("{0} sets them, and no config class reads them", file);
    }

    /// <summary>A key is read if it is a setting, an entry of a list or map setting, or a class above settings.</summary>
    private static bool Names(List<ServerSettingRegistry.Setting> settings, string path) =>
        settings.Any(x =>
            string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)
            || x.Path.StartsWith(path + ":", StringComparison.OrdinalIgnoreCase)
            || (
                x.Kind is "list" or "map" or "json"
                && path.StartsWith(x.Path + ":", StringComparison.OrdinalIgnoreCase)
            )
        );

    /// <summary>The path of each value in the file; a list, or an object with nothing in it, is one value.</summary>
    private static IEnumerable<string> Keys(JsonNode? node, string path) =>
        node is JsonObject { Count: > 0 } obj
            ? obj.SelectMany(x => Keys(x.Value, $"{path}:{x.Key}"))
            : [path];

    /// <summary>Every server config class that names its section, registered as a module registers it.</summary>
    private static ServiceCollection ConfigClasses()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();
        var configure = typeof(OptionsConfigurationServiceCollectionExtensions).GetMethod(
            nameof(OptionsConfigurationServiceCollectionExtensions.Configure),
            [typeof(IServiceCollection), typeof(IConfiguration)]
        )!;

        foreach (var file in Directory.GetFiles(AppContext.BaseDirectory, "Turbo.*.dll"))
        {
            var name = AssemblyName.GetAssemblyName(file);

            if (name.Name == typeof(ShippedAppSettingsTests).Assembly.GetName().Name)
                continue;

            Type[] types;

            try
            {
                types = Assembly.Load(name).GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = [.. ex.Types.OfType<Type>()];
            }

            foreach (
                var type in types.Where(x =>
                    x is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                    && WholeCollectionBinding.SectionOf(x) is { } section
                    && section.StartsWith("Turbo:", StringComparison.OrdinalIgnoreCase)
                )
            )
                configure
                    .MakeGenericMethod(type)
                    .Invoke(
                        null,
                        [
                            services,
                            configuration.GetSection(WholeCollectionBinding.SectionOf(type)!),
                        ]
                    );
        }

        return services;
    }
}
