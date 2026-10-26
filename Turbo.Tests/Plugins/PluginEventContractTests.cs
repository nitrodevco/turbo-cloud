using System.Collections.Concurrent;
using System.Runtime.Loader;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Events;
using Turbo.Events.Extensions;
using Turbo.Pipeline;
using Turbo.Primitives.Plugins.Events;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The contracts an event handler needs moved from <c>Turbo.Events</c> and <c>Turbo.Pipeline</c>
/// to <c>Turbo.Primitives</c>, and the two assemblies forward them: a plugin built before the
/// move, whose assembly names them in the old two, still loads, is found by the plugin scan and
/// hears what is published.
/// </summary>
public sealed class PluginEventContractTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_plugin_built_against_the_old_event_assemblies_still_hears_its_events()
    {
        // The two assemblies as they were before the move, under their names and versions.
        var pipeline = Compile(
            "Turbo.Pipeline",
            typeof(EnvelopeInvokerFactory<>).Assembly,
            """
            namespace Turbo.Pipeline.Registry
            {
                public interface IHandler<in TEnvelope, in TContext>
                {
                    System.Threading.Tasks.ValueTask HandleAsync(TEnvelope env, TContext ctx, System.Threading.CancellationToken ct);
                }

                public interface IBehavior<in TEnvelope, in TContext>
                {
                    System.Threading.Tasks.ValueTask InvokeAsync(TEnvelope env, TContext ctx, System.Func<System.Threading.Tasks.ValueTask> next, System.Threading.CancellationToken ct);
                }
            }

            namespace Turbo.Pipeline.Attributes
            {
                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
                public sealed class OrderAttribute(int value) : System.Attribute
                {
                    public int Value { get; } = value;
                }
            }
            """,
            Current()
        );
        var events = Compile(
            "Turbo.Events",
            typeof(EventSystem).Assembly,
            """
            extern alias today;

            namespace Turbo.Events.Registry
            {
                public sealed class EventContext { }

                public interface IEventHandler<in T> : Turbo.Pipeline.Registry.IHandler<T, EventContext>
                    where T : today::Turbo.Primitives.Events.IEvent;

                public interface IEventBehavior<in T> : Turbo.Pipeline.Registry.IBehavior<T, EventContext>
                    where T : today::Turbo.Primitives.Events.IEvent;
            }
            """,
            [.. Current(), pipeline.Reference]
        );
        var relay = Compile(
            "OldRelay",
            null,
            """
            extern alias today;

            using System;
            using System.Collections.Concurrent;
            using System.Threading;
            using System.Threading.Tasks;
            using Turbo.Events.Registry;
            using Turbo.Pipeline.Attributes;
            using today::Turbo.Primitives.Plugins.Events;

            public sealed class Relay(ConcurrentQueue<string> heard) : IEventHandler<PluginNoticeEvent>
            {
                public ValueTask HandleAsync(PluginNoticeEvent notice, EventContext ctx, CancellationToken ct)
                {
                    heard.Enqueue("handler " + notice.Key);
                    return ValueTask.CompletedTask;
                }
            }

            [Order(1)]
            public sealed class Guard(ConcurrentQueue<string> heard) : IEventBehavior<PluginNoticeEvent>
            {
                public ValueTask InvokeAsync(PluginNoticeEvent notice, EventContext ctx, Func<ValueTask> next, CancellationToken ct)
                {
                    heard.Enqueue("behaviour " + notice.Key);
                    return next();
                }
            }
            """,
            [.. Current(), pipeline.Reference, events.Reference]
        );
        var heard = new ConcurrentQueue<string>();
        var host = new ServiceCollection()
            .AddLogging()
            .AddTurboEventSystem()
            .BuildServiceProvider();
        var services = new ServiceCollection().AddSingleton(heard).BuildServiceProvider();
        // A plugin's own load context: the host's assemblies come from the host's.
        var context = new AssemblyLoadContext("old-relay", isCollectible: true);
        var plugin = context.LoadFromStream(new MemoryStream(relay.Image));

        plugin
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Should()
            .Contain(["Turbo.Events", "Turbo.Pipeline"], "it was built before the move");

        var registrations = new List<IDisposable>();

        foreach (var processor in host.GetServices<IAssemblyFeatureProcessor>())
            registrations.Add(await processor.ProcessAsync(plugin, services, Ct));

        await host.GetRequiredService<EventSystem>()
            .PublishAsync(
                new PluginNoticeEvent
                {
                    Source = "clienterrors",
                    Topic = "issue.new",
                    Title = "New client error #4",
                    Key = "4",
                },
                Ct
            );

        heard.Should().Equal("behaviour 4", "handler 4");

        foreach (var registration in registrations)
            registration.Dispose();

        context.Unload();
    }

    /// <summary>
    /// Every assembly the tests have loaded but the two that moved the contracts out. Today's
    /// <c>Turbo.Primitives</c> holds them too, so it is under the alias <c>today</c>: the old
    /// assemblies declare them, the plugin names those, and only its other types come from today's.
    /// </summary>
    private static List<MetadataReference> Current() =>
        [
            .. PluginBuilder
                .References()
                .Where(r =>
                    Path.GetFileNameWithoutExtension(r.Display)
                        is not ("Turbo.Events" or "Turbo.Pipeline")
                )
                .Select(r =>
                    Path.GetFileNameWithoutExtension(r.Display) == "Turbo.Primitives"
                        ? r.WithAliases(["today"])
                        : r
                ),
        ];

    private static (byte[] Image, MetadataReference Reference) Compile(
        string name,
        System.Reflection.Assembly? versionOf,
        string source,
        IEnumerable<MetadataReference> references
    )
    {
        var version = versionOf is null
            ? ""
            : $"[assembly: System.Reflection.AssemblyVersion(\"{versionOf.GetName().Version}\")]";
        var compilation = CSharpCompilation.Create(
            name,
            [
                CSharpSyntaxTree.ParseText(source, cancellationToken: Ct),
                CSharpSyntaxTree.ParseText(version, cancellationToken: Ct),
            ],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        using var dll = new MemoryStream();
        var emitted = compilation.Emit(dll, cancellationToken: Ct);

        emitted
            .Success.Should()
            .BeTrue(string.Join("; ", emitted.Diagnostics.Select(d => d.ToString())));

        return (dll.ToArray(), MetadataReference.CreateFromImage(dll.ToArray()));
    }
}
