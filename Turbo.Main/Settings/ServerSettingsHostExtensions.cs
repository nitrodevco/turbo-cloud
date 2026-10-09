using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Primitives.Settings;

namespace Turbo.Main.Settings;

internal static class ServerSettingsHostExtensions
{
    /// <summary>
    /// The admin panel's overrides, into the configuration. Before any module reads its options:
    /// they are read as the modules register.
    /// </summary>
    public static HostApplicationBuilder AddServerSettingsOverrides(
        this HostApplicationBuilder builder
    )
    {
        ServerSettingsConfiguration.AddTo(builder.Configuration);

        return builder;
    }

    /// <summary>
    /// The settings the panel shows and changes: those of every config class registered so far,
    /// so after every module.
    /// </summary>
    public static HostApplicationBuilder AddServerSettings(this HostApplicationBuilder builder)
    {
        builder.Services.Configure<ServerSettingsConfig>(
            builder.Configuration.GetSection(ServerSettingsConfig.SECTION_NAME)
        );
        builder.Services.AddSingleton(ServerSettingRegistry.From(builder.Services));
        builder.Services.AddSingleton<ServerSettingsService>();
        builder.Services.AddSingleton<IServerSettings>(sp =>
            sp.GetRequiredService<ServerSettingsService>()
        );

        return builder;
    }
}
