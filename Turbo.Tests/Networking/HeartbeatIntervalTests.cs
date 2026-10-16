using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Turbo.Networking.Configuration;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// Habbo's server pings a connection every 60 to 90 seconds (median 60 s over three recorded
/// official sessions), not every 10. The silence timeout stays several pings long, so one late
/// Pong does not disconnect anybody.
/// </summary>
public sealed class HeartbeatIntervalTests
{
    [Fact]
    public void TheDefaults_PingEveryMinute_AndWaitThreePingsForSilence()
    {
        var config = new NetworkingConfig();

        config.PingIntervalMilliseconds.Should().Be(60000);
        config.SessionTimeoutMilliseconds.Should().Be(180000);
    }

    [Fact]
    public void AppSettings_PingEveryMinute_AndWaitThreePingsForSilence()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "appsettings.json"
        );
        var config = new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build()
            .GetSection(NetworkingConfig.SECTION_NAME)
            .Get<NetworkingConfig>()!;

        config.PingIntervalMilliseconds.Should().Be(60000);
        config.SessionTimeoutMilliseconds.Should().Be(180000);
    }
}
