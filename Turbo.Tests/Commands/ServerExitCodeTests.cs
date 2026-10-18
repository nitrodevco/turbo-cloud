using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Main;
using Turbo.Operations.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// What the server process ends with: a host that starts it again only when it fails
/// (Supervisor's <c>autorestart=unexpected</c>) must see a crash as one.
/// </summary>
public sealed class ServerExitCodeTests
{
    [Fact]
    public async Task a_server_that_crashes_as_it_starts_exits_with_a_failure_not_a_restart()
    {
        // No database to migrate: the server can't come up.
        using var host = new HostBuilder().Build();
        var log = new CapturingLogger<ServerExitCodeTests>();

        var code = await Program.RunAsync(host, log, TestContext.Current.CancellationToken);

        code.Should().NotBe(0);
        code.Should().NotBe(new OperationsConfig().RestartExitCode);
        log.AtLeast(LogLevel.Critical)
            .Should()
            .ContainSingle(x => x.Message.Contains("Host terminated unexpectedly"));
    }
}
