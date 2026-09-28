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
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// Holds every packet handler to what <see cref="RequiresPermissionAttribute"/> declares: a
/// handler with the attribute must call <see cref="GrainFactoryExtensions.HasPermissionAsync"/>
/// with each node it names, and a handler without it must not call it at all, so the declaration
/// a reviewer reads and the check that runs cannot drift apart. It reads the compiled IL of
/// <c>HandleAsync</c> (the async state machine's <c>MoveNext</c> when there is one): a
/// <c>PermissionNodes</c> constant compiles to the string itself.
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
    public void Handler_ChecksExactlyWhatItDeclares(Type handler)
    {
        var declared = handler.GetCustomAttribute<RequiresPermissionAttribute>()?.Nodes ?? [];
        var (strings, calls) = ReadHandleAsync(handler);
        var checks = calls.Contains(HAS_PERMISSION);

        if (declared.Count == 0)
        {
            checks
                .Should()
                .BeFalse(
                    "{0} checks a permission without declaring it with [RequiresPermission]",
                    handler.Name
                );

            return;
        }

        checks
            .Should()
            .BeTrue("{0} declares [RequiresPermission] but never checks it", handler.Name);

        foreach (var node in declared)
        {
            REGISTRY.IsRegistered(node).Should().BeTrue("{0} names {1}", handler.Name, node);
            strings
                .Should()
                .Contain(node, "{0} declares {1} but does not check it", handler.Name, node);
        }
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
    private static (HashSet<string> Strings, HashSet<MethodBase> Calls) ReadHandleAsync(
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

        var il = body.GetMethodBody()!.GetILAsByteArray()!;
        var module = body.Module;
        var strings = new HashSet<string>(StringComparer.Ordinal);
        var calls = new HashSet<MethodBase>();

        // A byte-level scan rather than a full decoder: an ldstr (0x72) or call/callvirt
        // (0x28/0x6F) byte followed by a token of the right table is taken as that instruction.
        // A stray match can only add to what is found, never hide a call that is there.
        for (var i = 0; i + 4 < il.Length; i++)
        {
            var token = BitConverter.ToInt32(il, i + 1);
            var table = (uint)token >> 24;

            if (il[i] == 0x72 && table == 0x70)
            {
                try
                {
                    strings.Add(module.ResolveString(token));
                }
                catch (ArgumentException) { }
            }
            else if ((il[i] == 0x28 || il[i] == 0x6F) && table is 0x06 or 0x0A or 0x2B)
            {
                try
                {
                    if (module.ResolveMethod(token) is { } called)
                        calls.Add(called);
                }
                catch (ArgumentException) { }
                catch (BadImageFormatException) { }
            }
        }

        return (strings, calls);
    }
}
