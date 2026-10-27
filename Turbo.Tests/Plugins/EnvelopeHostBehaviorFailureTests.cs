using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Pipeline;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// A behavior is how a plugin stops a packet or an event before its handlers, so a broken one must
/// stop it too (fail closed): one that throws before passing the envelope on, or that cannot be
/// built, drops it, and the log says so.
/// </summary>
public sealed class EnvelopeHostBehaviorFailureTests
{
    private readonly CapturingLogger<EnvelopeHostBehaviorFailureTests> _log = new();
    private int _handled;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_behavior_that_throws_before_passing_it_on_drops_the_envelope()
    {
        var host = HostWithHandler();

        using var behavior = host.RegisterBehavior(
            typeof(string),
            typeof(object),
            new ServiceCollection().BuildServiceProvider(),
            _ => new object(),
            (_, _, _, _, _) => throw new InvalidOperationException("guard down"),
            0
        );

        await host.PublishAsync("buy", null, Ct);

        _handled.Should().Be(0);
        _log.AtLeast(LogLevel.Error)
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain("dropped");
    }

    // Was passed on: a behavior that could not be built let the envelope through to the handlers.
    [Fact]
    public async Task A_behavior_that_cannot_be_built_drops_the_envelope()
    {
        var host = HostWithHandler();

        using var behavior = host.RegisterBehavior(
            typeof(string),
            typeof(object),
            new ServiceCollection().BuildServiceProvider(),
            _ => throw new InvalidOperationException("missing service"),
            (_, _, _, next, _) => next(),
            0
        );

        await host.PublishAsync("buy", null, Ct);

        _handled.Should().Be(0);
        _log.AtLeast(LogLevel.Error)
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain("dropped");
    }

    private EnvelopeHost<object, object, object> HostWithHandler()
    {
        var host = new EnvelopeHost<object, object, object>(
            new ServiceCollection().BuildServiceProvider(),
            new EnvelopeHostOptions<object, object, object>
            {
                CreateContextAsync = (_, _) => Task.FromResult(new object()),
            },
            _log
        );

        host.RegisterHandler(
            typeof(string),
            typeof(object),
            new ServiceCollection().BuildServiceProvider(),
            _ => new object(),
            (_, _, _, _) =>
            {
                _handled++;

                return ValueTask.CompletedTask;
            }
        );

        return host;
    }
}
