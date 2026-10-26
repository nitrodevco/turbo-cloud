using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Events.Extensions;
using Turbo.Events.Registry;
using Turbo.Primitives.Events;
using Turbo.Primitives.Plugins.Events;
using Turbo.Runtime.AssemblyProcessing;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// One plugin tells others something through <see cref="PluginNoticeEvent"/>: a relay's handler,
/// found by the same assembly scan a plugin load runs, hears what another publishes on the host's
/// event system, and stops hearing once its registrations are disposed (the plugin unloads).
/// </summary>
public sealed class PluginNoticeEventTests
{
    [Fact]
    public async Task A_relay_found_by_the_plugin_scan_hears_a_notice_until_it_unloads()
    {
        var sink = new NoticeSink();
        var host = new ServiceCollection()
            .AddLogging()
            .AddTurboEventSystem()
            .BuildServiceProvider();
        var relay = new ServiceCollection().AddSingleton(sink).BuildServiceProvider();
        // What a plugin publishes through: the contract, which the host's event system is.
        var events = host.GetRequiredService<IEventPublisher>();
        var notice = new PluginNoticeEvent
        {
            Source = "clienterrors",
            Topic = "issue.new",
            Title = "New client error #4",
            Url = "https://panel.example/plugin/clienterrors/issue/4",
            Fields = [new("Build", "a1b2c3"), new("Players", "2")],
            Level = PluginNoticeLevel.Error,
            Key = "4",
        };

        // A plugin's load: every feature processor scans its assembly with its own services.
        var registrations = new List<IDisposable>();

        foreach (var processor in host.GetServices<IAssemblyFeatureProcessor>())
            registrations.Add(
                await processor.ProcessAsync(
                    typeof(RecordingNoticeRelay).Assembly,
                    relay,
                    TestContext.Current.CancellationToken
                )
            );

        await events.PublishAsync(notice, TestContext.Current.CancellationToken);

        sink.Notices.Should().ContainSingle().Which.Should().BeSameAs(notice);

        foreach (var registration in registrations)
            registration.Dispose();

        await events.PublishAsync(notice with { Key = "5" }, TestContext.Current.CancellationToken);

        sink.Notices.Should().ContainSingle("an unloaded relay hears nothing");
    }

    [Fact]
    public void The_notice_is_the_hosts_type_so_every_plugin_sees_the_same_one()
    {
        // Plugins load in load contexts of their own and share only the host's assemblies.
        typeof(PluginNoticeEvent).Assembly.Should().BeSameAs(typeof(IEvent).Assembly);
        typeof(PluginNoticeField).Assembly.Should().BeSameAs(typeof(IEvent).Assembly);
        new PluginNoticeEvent
        {
            Source = "bugreport",
            Topic = "report.new",
            Title = "x",
        }
            .Fields.IsDefault.Should()
            .BeFalse("a notice without fields has an empty list, not a default one");
    }
}

public sealed class NoticeSink
{
    public ConcurrentQueue<PluginNoticeEvent> Notices { get; } = new();
}

/// <summary>A relay as a plugin declares one: found by the scan, built from the plugin's services.</summary>
public sealed class RecordingNoticeRelay(NoticeSink sink) : IEventHandler<PluginNoticeEvent>
{
    public ValueTask HandleAsync(PluginNoticeEvent env, EventContext ctx, CancellationToken ct)
    {
        sink.Notices.Enqueue(env);

        return ValueTask.CompletedTask;
    }
}
