using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Orleans.Serialization;
using Turbo.Main.Extensions;
using Turbo.Primitives.Rooms.Snapshots;
using Xunit;

namespace Turbo.Tests.Networking;

public class TelemetryHostingTests
{
    [Fact]
    public void DisabledTelemetry_DoesNotRegisterProvidersOrRequireAnEndpoint()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Turbo:Telemetry:Enabled"] = "false",
                ["Turbo:Telemetry:Endpoint"] = "not a URI",
            }
        );

        builder.AddTurboTelemetry();

        Assert.DoesNotContain(builder.Services, s => s.ServiceType == typeof(TracerProvider));
        Assert.DoesNotContain(builder.Services, s => s.ServiceType == typeof(MeterProvider));
    }

    [Fact]
    public void PrivacyProcessor_RemovesExceptionTextAndRetainsErrorType()
    {
        using var activity = new Activity("failed-call").Start();
        activity.SetStatus(ActivityStatusCode.Error, "private message");
        foreach (
            var key in new[]
            {
                "exception.message",
                "exception.stacktrace",
                "exception.stack_trace",
                "error.message",
            }
        )
            activity.SetTag(key, "private message");
        activity.SetTag("exception.type", "InvalidOperationException");
        using var processor = new TelemetryPrivacyProcessor();
        processor.OnEnd(activity);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.StatusDescription);
        Assert.Equal("InvalidOperationException", activity.GetTagItem("exception.type"));
        Assert.DoesNotContain(
            activity.TagObjects,
            tag => tag.Value?.ToString() == "private message"
        );
    }

    [Theory]
    [InlineData("Endpoint", "not a URI")]
    [InlineData("Endpoint", "file:///tmp/telemetry")]
    [InlineData("TraceSampleRatio", "-1")]
    [InlineData("TraceSampleRatio", "1.1")]
    [InlineData("TraceSampleRatio", "NaN")]
    [InlineData("ServiceName", "")]
    public void EnabledTelemetry_RejectsInvalidConfiguration(string key, string value)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Turbo:Telemetry:Enabled"] = "true",
                [$"Turbo:Telemetry:{key}"] = value,
            }
        );

        Assert.Throws<InvalidOperationException>(() => builder.AddTurboTelemetry());
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(638950000000000000L)]
    public void RoomStreamSnapshot_RetainsOptionalPublicationTime(long timestamp)
    {
        var services = new ServiceCollection();
        services.AddSerializer();
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<Serializer>();
        var original = new RoomOutboundSnapshot
        {
            RoomId = 1,
            Composers = [],
            PublishedAtUtcTicks = timestamp,
        };

        var decoded = serializer.Deserialize<RoomOutboundSnapshot>(
            serializer.SerializeToArray(original)
        );

        Assert.NotNull(decoded);
        Assert.Equal(timestamp, decoded.PublishedAtUtcTicks);
        Assert.Equal(original.RoomId, decoded.RoomId);
        Assert.Empty(decoded.Composers);
    }
}
