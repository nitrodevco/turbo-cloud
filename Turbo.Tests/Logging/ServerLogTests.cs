using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Logging.Extensions;
using Turbo.Primitives.Logging;
using Xunit;

namespace Turbo.Tests.Logging;

/// <summary>
/// <see cref="IServerLog"/> is what a developer's tools read instead of the console (the MCP
/// plugin's <c>server_log</c> and <c>plugin_errors</c>): it must be given what the server's logging
/// is given, and keep only the newest entries.
/// </summary>
public sealed class ServerLogTests
{
    [Fact]
    public void What_the_server_logs_reaches_its_server_log_newest_first()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddTurboLogging(builder);

        using var host = builder.Build();
        var logger = host
            .Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Skills:Skills.SkillBook");
        var log = host.Services.GetRequiredService<IServerLog>();

        logger.LogInformation("Trained {Player}", 7);
        logger.LogError(
            new InvalidOperationException("No book."),
            "Training failed for {Player}",
            7
        );
        logger.LogDebug("Below the default level");

        log.Recent(10).Select(x => x.Message).Should().Equal("Training failed for 7", "Trained 7");
        log.Recent(10, LogLevel.Error)
            .Should()
            .ContainSingle()
            .Which.Exception.Should()
            .Contain("No book.");
        log.Recent(10, contains: "skills:").Should().HaveCount(2);
        log.LastError!.Message.Should().Be("Training failed for 7");
    }

    [Fact]
    public void Only_the_newest_entries_are_kept_and_an_error_goes_with_them()
    {
        var log = new ServerLog(capacity: 3);
        var logger = log.CreateLogger("Test");

        logger.LogError("first");

        foreach (var n in Enumerable.Range(1, 3))
            logger.LogInformation("line {N}", n);

        log.Recent(10)
            .Select(x => (x.Sequence, x.Message))
            .Should()
            .Equal((4L, "line 3"), (3L, "line 2"), (2L, "line 1"));
        log.LastError.Should().BeNull("the error is no longer kept");
    }
}
