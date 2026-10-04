using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Hosting;
using Turbo.Main.Configuration;
using Turbo.Primitives.Orleans;

namespace Turbo.Main.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static HostApplicationBuilder AddOrleans(this HostApplicationBuilder builder)
    {
        var orleansConfig =
            builder.Configuration.GetSection(OrleansConfig.SECTION_NAME).Get<OrleansConfig>()
            ?? new OrleansConfig();

        builder.UseOrleans(
            (System.Action<ISiloBuilder>)(
                silo =>
                {
                    silo.Configure<GrainCollectionOptions>(options =>
                    {
                        options.CollectionAge = TimeSpan.FromMinutes(
                            orleansConfig.GrainCollectionAgeMinutes
                        );
                    });
                    var telemetryConfig =
                        builder
                            .Configuration.GetSection(TelemetryConfig.SECTION_NAME)
                            .Get<TelemetryConfig>()
                        ?? new TelemetryConfig();
                    if (telemetryConfig.Enabled)
                    {
                        silo.AddActivityPropagation();
                    }

                    silo.ConfigureEndpoints(
                        orleansConfig.SiloAddress,
                        siloPort: orleansConfig.SiloPort,
                        gatewayPort: orleansConfig.GatewayPort,
                        listenOnAnyHostAddress: true
                    );

                    silo.UseLocalhostClustering(
                            siloPort: orleansConfig.SiloPort,
                            gatewayPort: orleansConfig.GatewayPort
                        )
                        .AddMemoryGrainStorage(OrleansStorageNames.PUB_SUB_STORE)
                        .AddMemoryGrainStorage(OrleansStorageNames.PLAYER_STORE)
                        .AddMemoryGrainStorage(OrleansStorageNames.ROOM_STORE)
                        .AddMemoryStreams(
                            OrleansStreamProviders.DEFAULT_STREAM_PROVIDER,
                            streams => ConfigureStreamCache(streams, orleansConfig)
                        )
                        .AddMemoryStreams(
                            OrleansStreamProviders.ROOM_STREAM_PROVIDER,
                            streams =>
                            {
                                ConfigureStreamCache(streams, orleansConfig);
                                streams.ConfigurePullingAgent(ob =>
                                    ob.Configure(options =>
                                    {
                                        // Memory streams are pull-based; the default 100ms poll
                                        // adds up to 100ms of jitter to every room packet, which
                                        // is visible in the avatar walk cadence.
                                        options.GetQueueMsgsTimerPeriod = TimeSpan.FromMilliseconds(
                                            orleansConfig.RoomStreamPollMs
                                        );
                                    })
                                );
                            }
                        );
                }
            )
        );

        return builder;
    }

    // The cache keeps delivered messages for consumers that rewind; nothing here does, and at
    // Orleans' defaults (five to thirty minutes) a load test held over 600 MB of room traffic
    // that every subscriber had long since received.
    private static void ConfigureStreamCache(
        ISiloMemoryStreamConfigurator streams,
        OrleansConfig orleansConfig
    ) =>
        streams.ConfigureCacheEviction(ob =>
            ob.Configure(options =>
            {
                options.DataMinTimeInCache = TimeSpan.FromSeconds(
                    orleansConfig.StreamCacheMinSeconds
                );
                options.DataMaxAgeInCache = TimeSpan.FromSeconds(
                    orleansConfig.StreamCacheMaxSeconds
                );
                options.MetadataMinTimeInCache = TimeSpan.FromSeconds(
                    orleansConfig.StreamCacheMaxSeconds
                );
            })
        );
}
