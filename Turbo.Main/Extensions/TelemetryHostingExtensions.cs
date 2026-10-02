using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Orleans.Diagnostics;
using Turbo.Main.Configuration;

namespace Turbo.Main.Extensions;

public static class TelemetryHostingExtensions
{
    public static HostApplicationBuilder AddTurboTelemetry(this HostApplicationBuilder builder)
    {
        var config =
            builder.Configuration.GetSection(TelemetryConfig.SECTION_NAME).Get<TelemetryConfig>()
            ?? new TelemetryConfig();

        if (!config.Enabled)
        {
            return builder;
        }

        config.Validate();
        var endpoint = new Uri(config.Endpoint, UriKind.Absolute);

        builder
            .Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(config.ServiceName))
            .WithTracing(tracing =>
                tracing
                    .SetSampler(
                        new ParentBasedSampler(
                            new TraceIdRatioBasedSampler(config.TraceSampleRatio)
                        )
                    )
                    .AddSource(Turbo.Primitives.Rooms.RoomTelemetry.SOURCE_NAME)
                    .AddSource(ActivitySources.ApplicationGrainActivitySourceName)
                    .AddSource(ActivitySources.LifecycleActivitySourceName)
                    .AddProcessor<TelemetryPrivacyProcessor>()
                    .AddOtlpExporter(options => options.Endpoint = endpoint)
            )
            .WithMetrics(metrics =>
                metrics
                    .AddMeter(Turbo.Primitives.Rooms.RoomTelemetry.SOURCE_NAME)
                    .AddMeter("Microsoft.Orleans")
                    .AddMeter("System.Runtime")
                    .AddOtlpExporter(options => options.Endpoint = endpoint)
            );

        return builder;
    }
}
