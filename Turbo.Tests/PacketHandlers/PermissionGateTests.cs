using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// Holds every packet handler to how <see cref="RequiresPermissionAttribute"/> works: the pipeline
/// enforces the attribute (<see cref="PermissionGate"/>), so every node a handler declares must be
/// registered, and no handler calls <see cref="GrainFactoryExtensions.HasPermissionAsync"/> itself.
/// A handler-level gate is the attribute or nothing, so what a reviewer reads is what runs. It
/// reads the compiled IL of <c>HandleAsync</c> (the async state machine's <c>MoveNext</c> when
/// there is one).
/// </summary>
public class PermissionGateTests
{
    private static readonly MethodInfo HAS_PERMISSION = typeof(GrainFactoryExtensions).GetMethod(
        nameof(GrainFactoryExtensions.HasPermissionAsync)
    )!;

    private static readonly PermissionRegistry REGISTRY = new([new CorePermissionNodeSource()]);

    [Fact]
    public void Every_handler_declares_registered_nodes_and_leaves_its_gate_to_the_pipeline()
    {
        var offenders = HandlerTypes()
            .SelectMany(handler =>
                (handler.GetCustomAttribute<RequiresPermissionAttribute>()?.Nodes ?? [])
                    .Where(node => !REGISTRY.IsRegistered(node))
                    .Select(node => $"{handler.Name} names {node}, which is not registered")
                    .Concat(
                        ReadHandleAsyncBody(handler).Calls.Contains(HAS_PERMISSION)
                            ?
                            [
                                $"{handler.Name} checks a permission itself; declare it with [RequiresPermission] instead",
                            ]
                            : []
                    )
            );

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void TheScanFindsGatedHandlers()
    {
        // Guards against the theory passing because it read nothing.
        HandlerTypes()
            .Where(x => x.GetCustomAttribute<RequiresPermissionAttribute>() is not null)
            .Should()
            .Contain(typeof(ToggleStaffPickMessageHandler))
            .And.HaveCountGreaterThan(20);
    }

    internal static IEnumerable<Type> HandlerTypes() =>
        typeof(ToggleStaffPickMessageHandler)
            .Assembly.GetTypes()
            .Where(x =>
                x is { IsClass: true, IsAbstract: false }
                && x.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>)
                    )
            )
            .OrderBy(x => x.FullName, StringComparer.Ordinal);

    /// <summary>The string literals and the methods <c>HandleAsync</c> loads and calls.</summary>
    internal static (HashSet<string> Strings, HashSet<MethodBase> Calls) ReadHandleAsyncBody(
        Type handler
    )
    {
        var method = handler.GetMethod("HandleAsync")!;
        var body = method.GetCustomAttribute<AsyncStateMachineAttribute>() is { } machine
            ? machine.StateMachineType.GetMethod(
                "MoveNext",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            )!
            : method;

        var strings = new HashSet<string>(StringComparer.Ordinal);
        var calls = new HashSet<MethodBase>();

        IlScanner.Scan(body, strings, calls);

        return (strings, calls);
    }
}
