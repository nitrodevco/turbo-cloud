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

    public static TheoryData<Type> Handlers()
    {
        var data = new TheoryData<Type>();

        foreach (var type in HandlerTypes())
            data.Add(type);

        return data;
    }

    [Theory]
    [MemberData(nameof(Handlers))]
    public void Handler_DeclaresOnlyRegisteredNodes(Type handler)
    {
        var declared = handler.GetCustomAttribute<RequiresPermissionAttribute>()?.Nodes ?? [];

        foreach (var node in declared)
            REGISTRY.IsRegistered(node).Should().BeTrue("{0} names {1}", handler.Name, node);
    }

    [Theory]
    [MemberData(nameof(Handlers))]
    public void Handler_LeavesItsGateToThePipeline(Type handler)
    {
        var (_, calls) = ReadHandleAsyncBody(handler);

        calls
            .Should()
            .NotContain(
                HAS_PERMISSION,
                "{0} checks a permission itself; declare it with [RequiresPermission] instead",
                handler.Name
            );
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

    private static IEnumerable<Type> HandlerTypes() =>
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
    private static (HashSet<string> Strings, HashSet<MethodBase> Calls) ReadHandleAsyncBody(
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
