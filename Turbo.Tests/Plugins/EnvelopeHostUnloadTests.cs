using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Pipeline;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The packet and event registries are envelope hosts keyed by the envelope's type. A plugin's
/// messages and events are its own types, so the host must let go of a type once its last handler
/// or behavior is taken out: the key alone keeps the type, its assembly and the plugin's whole
/// load context alive, and every reload of a plugin with a packet handler leaked the version
/// before ("ALC ... did not unload within timeout").
/// </summary>
[Collection("Plugin unload")]
public sealed class EnvelopeHostUnloadTests
{
    private const string SOURCE = "public sealed class PluginPing { }";

    [Fact]
    public async Task A_type_whose_handler_is_taken_out_does_not_keep_its_load_context()
    {
        var host = NewHost();
        var context = RegisterAndRemove(host, handler: true);

        // The host is still alive and used, as the server's registries are.
        await host.PublishAsync(new object(), null, TestContext.Current.CancellationToken);

        Collected(context)
            .Should()
            .BeTrue("nothing should keep a plugin's type once its handler is gone");
    }

    [Fact]
    public async Task A_type_whose_behavior_is_taken_out_does_not_keep_its_load_context()
    {
        var host = NewHost();
        var context = RegisterAndRemove(host, handler: false);

        await host.PublishAsync(new object(), null, TestContext.Current.CancellationToken);

        Collected(context)
            .Should()
            .BeTrue("nothing should keep a plugin's type once its behavior is gone");
    }

    [Fact]
    public async Task A_handler_registered_again_after_the_last_was_taken_out_is_reached()
    {
        var host = NewHost();
        var calls = 0;

        host.RegisterHandler(
                typeof(string),
                typeof(object),
                new ServiceCollection().BuildServiceProvider(),
                _ => new object(),
                (_, _, _, _) => ValueTask.CompletedTask
            )
            .Dispose();
        using var again = host.RegisterHandler(
            typeof(string),
            typeof(object),
            new ServiceCollection().BuildServiceProvider(),
            _ => new object(),
            (_, _, _, _) =>
            {
                calls++;

                return ValueTask.CompletedTask;
            }
        );

        await host.PublishAsync("ping", null, TestContext.Current.CancellationToken);

        calls.Should().Be(1);
    }

    private static EnvelopeHost<object, object, object> NewHost() =>
        new(
            new ServiceCollection().BuildServiceProvider(),
            new EnvelopeHostOptions<object, object, object>
            {
                CreateContextAsync = (_, _) => Task.FromResult(new object()),
            },
            NullLogger.Instance
        );

    /// <summary>A collectible context holding one type, registered with the host and taken out again, then unloaded.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RegisterAndRemove(
        EnvelopeHost<object, object, object> host,
        bool handler
    )
    {
        var context = new AssemblyLoadContext("envelope-host-unload", isCollectible: true);
        var type = context
            .LoadFromStream(
                new MemoryStream(PluginBuilder.Compile("EnvelopeHostUnloadPlugin", SOURCE))
            )
            .GetType("PluginPing", throwOnError: true)!;
        var services = new ServiceCollection().BuildServiceProvider();

        using (
            handler
                ? host.RegisterHandler(
                    type,
                    type,
                    services,
                    _ => new object(),
                    (_, _, _, _) => ValueTask.CompletedTask
                )
                : host.RegisterBehavior(
                    type,
                    type,
                    services,
                    _ => new object(),
                    (_, _, _, next, _) => next(),
                    0
                )
        ) { }

        context.Unload();

        return new WeakReference(context);
    }

    private static bool Collected(WeakReference context)
    {
        for (var i = 0; (i < 20) && context.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return !context.IsAlive;
    }
}
