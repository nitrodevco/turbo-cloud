using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Turbo.Runtime.Configuration;

namespace Turbo.Main.Settings;

/// <summary>
/// What a restart would make of the panel's overrides: the configuration it would read with a
/// given set of them, and what the validators of a config class (<c>.Validate(...)</c>, refused
/// at start by <c>ValidateOnStart</c>) would say of the options built from it.
/// </summary>
internal static class ServerSettingsCheck
{
    private static readonly MethodInfo FAILURES_OF = typeof(ServerSettingsCheck).GetMethod(
        nameof(FailuresOf),
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    /// <summary>Whether the options of <paramref name="type"/> have validators of their own.</summary>
    public static bool Validates(IServiceProvider services, Type type) =>
        services.GetService(
            typeof(IEnumerable<>).MakeGenericType(typeof(IValidateOptions<>).MakeGenericType(type))
        )
            is IEnumerable<object> validators
        && validators.Any();

    /// <summary>
    /// The configuration a restart would read with <paramref name="rows"/> as the panel's
    /// overrides: every other layer as it is now, the lists and maps the rows override hidden
    /// below them. Dispose it when done.
    /// </summary>
    public static ConfigurationRoot Configuration(
        IConfiguration configuration,
        IReadOnlyList<(string Path, string Value)> rows
    )
    {
        var overrides = new ServerSettingsOverrides();
        var panel = new ServerSettingsConfigurationProvider(
            () => new ServerSettingsLoad(rows, null),
            overrides
        );
        var providers = ((configuration as IConfigurationRoot)?.Providers ?? [])
            .Select(provider =>
                provider switch
                {
                    ServerSettingsConfigurationProvider => (IConfigurationProvider)panel,
                    ShadowedConfigurationProvider shadowed => new ShadowedConfigurationProvider(
                        new Loaded(shadowed.Inner),
                        overrides
                    ),
                    _ => new Loaded(provider),
                }
            )
            .ToList();

        // A host built without the overrides among its layers: they go on top.
        if (!providers.Contains(panel))
            providers.Add(panel);

        return new ConfigurationRoot(providers);
    }

    /// <summary>
    /// What the validators of <paramref name="type"/> say of its options built from
    /// <paramref name="configuration"/> through every step the server's own go through (binding
    /// the section, what modules configure after it, lists and maps taken whole); empty when they
    /// pass. A value the binder can't read is a failure too.
    /// </summary>
    public static IReadOnlyList<string> Failures(
        IServiceProvider services,
        Type type,
        IConfigurationRoot configuration,
        string section
    ) =>
        (IReadOnlyList<string>)
            FAILURES_OF
                .MakeGenericMethod(type)
                .Invoke(
                    null,
                    BindingFlags.DoNotWrapExceptions,
                    null,
                    [services, configuration, section],
                    null
                )!;

    private static IReadOnlyList<string> FailuresOf<T>(
        IServiceProvider services,
        IConfigurationRoot configuration,
        string section
    )
        where T : class
    {
        var bound = configuration.GetSection(section);
        // The container's steps read the container's configuration; those are read from this one.
        var factory = new OptionsFactory<T>(
            services
                .GetServices<IConfigureOptions<T>>()
                .Select(step =>
                    step switch
                    {
                        NamedConfigureFromConfigurationOptions<T> binding => (IConfigureOptions<T>)
                            new ConfigureNamedOptions<T>(binding.Name, x => bound.Bind(x)),
                        ConfigureNamedOptions<T, IConfiguration> fromContainer =>
                            new ConfigureNamedOptions<T, IConfiguration>(
                                fromContainer.Name,
                                configuration,
                                fromContainer.Action
                            ),
                        _ => step,
                    }
                ),
            services
                .GetServices<IPostConfigureOptions<T>>()
                .Select(step =>
                    WholeCollectionBinding.IsRebind(step)
                        ? new PostConfigureOptions<T>(
                            Options.DefaultName,
                            x => WholeCollectionBinding.Rebind(x, configuration, bound)
                        )
                        : step
                ),
            services.GetServices<IValidateOptions<T>>()
        );

        try
        {
            factory.Create(Options.DefaultName);

            return [];
        }
        catch (OptionsValidationException ex)
        {
            return [.. ex.Failures];
        }
        catch (InvalidOperationException ex)
        {
            return [ex.Message];
        }
    }

    /// <summary>
    /// A layer of the configuration as it is loaded now: building the configuration over it
    /// doesn't read its file or the environment again, nor tell anything it reloaded.
    /// </summary>
    private sealed class Loaded(IConfigurationProvider inner) : IConfigurationProvider
    {
        private static readonly IChangeToken NEVER = new CancellationChangeToken(
            CancellationToken.None
        );

        public bool TryGet(string key, out string? value) => inner.TryGet(key, out value);

        public void Set(string key, string? value) { }

        public IChangeToken GetReloadToken() => NEVER;

        public void Load() { }

        public IEnumerable<string> GetChildKeys(
            IEnumerable<string> earlierKeys,
            string? parentPath
        ) => inner.GetChildKeys(earlierKeys, parentPath);
    }
}
