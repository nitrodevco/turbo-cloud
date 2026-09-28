using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

/// <summary>
/// A registered node that nothing on the server reads is a gate somebody meant to write and did
/// not — the failure a permission system invites (<c>docs/permissions.md</c> §14, phase 8). This
/// reads the IL of every server assembly for each node's string (a <c>PermissionNodes</c> constant
/// compiles to the string itself) and fails for a node that appears nowhere but its own
/// registration, unless it is listed below with the reason nothing on the server can read it.
/// </summary>
public class PermissionNodeReaderTests
{
    /// <summary>
    /// Nodes with no server reader on purpose. Each is only projected to the client — as a perk,
    /// or through the security level its client level raises — or waits on a feature not built.
    /// </summary>
    private static readonly Dictionary<string, string> UNREAD = new(StringComparer.Ordinal)
    {
        [PermissionNodes.Catalog.BUILDERS_CLUB_WITHOUT_MEMBERSHIP] =
            "the client alone gates opening the Builders Club catalog, at the level this projects to",
        [PermissionNodes.Chat.FURNI_CHOOSER] =
            "the :furni chooser is the client's, at the level this projects to",
        [PermissionNodes.Catalog.GIFT_HIDE_SENDER] =
            "buying a gift is a stub; it gates ShowPurchaserName once gifts are built",
        [PermissionNodes.Permissions.MANAGE] = "waits on an in-game permission editor",
        [PermissionNodes.Perk.CAMERA] = "projected to a perk",
        [PermissionNodes.Perk.MOUSE_ZOOM] = "projected to a perk",
        [PermissionNodes.Perk.CITIZEN] = "projected to a perk",
        [PermissionNodes.Perk.NAVIGATOR_THUMBNAIL_CAMERA] = "projected to a perk",
        [PermissionNodes.Perk.NAVIGATOR_PHASE_ONE] = "projected to a perk",
        [PermissionNodes.Perk.NAVIGATOR_PHASE_TWO] = "projected to a perk",
        [PermissionNodes.Perk.GUIDE_TOOL] = "projected to a perk",
        [PermissionNodes.Perk.JUDGE_CHAT_REVIEWS] = "projected to a perk",
        [PermissionNodes.Perk.CALL_ON_HELPERS] = "projected to a perk",
        [PermissionNodes.Perk.VOTE_IN_COMPETITIONS] = "projected to a perk",
        [PermissionNodes.Perk.HABBO_CLUB_OFFER_BETA] = "projected to a perk",
        [PermissionNodes.Perk.NO_VIDEO_OFFERS] =
            "the client turns video offers off at the level this projects to",
    };

    private static readonly PermissionRegistry REGISTRY = new([new CorePermissionNodeSource()]);

    private static readonly Lazy<HashSet<string>> READ = new(ReadStrings);

    [Fact]
    public void EveryNode_IsReadSomewhere_OrListedWithAReason()
    {
        var unread = REGISTRY
            .Nodes.Keys.Where(x => !READ.Value.Contains(x) && !UNREAD.ContainsKey(x))
            .Order(StringComparer.Ordinal)
            .ToList();

        unread
            .Should()
            .BeEmpty(
                "a registered node nothing reads is a gate nobody wrote; unread: {0}",
                string.Join(", ", unread)
            );
    }

    [Fact]
    public void EveryMetaKey_IsReadSomewhere()
    {
        var unread = REGISTRY
            .MetaKeys.Keys.Where(x => !READ.Value.Contains(x))
            .Order(StringComparer.Ordinal)
            .ToList();

        unread
            .Should()
            .BeEmpty(
                "a registered meta key nothing reads sets nothing; unread: {0}",
                string.Join(", ", unread)
            );
    }

    [Fact]
    public void TheList_OnlyHoldsRegisteredNodesThatAreStillUnread()
    {
        UNREAD.Keys.Should().OnlyContain(x => REGISTRY.IsRegistered(x));
        UNREAD
            .Keys.Where(x => READ.Value.Contains(x))
            .Should()
            .BeEmpty("a node that has gained a reader should come off the list");
    }

    /// <summary>
    /// Every string literal in every server assembly, except where core registers its own nodes,
    /// which is not a read.
    /// </summary>
    private static HashSet<string> ReadStrings()
    {
        var strings = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.GetFiles(AppContext.BaseDirectory, "Turbo.*.dll"))
        {
            var name = AssemblyName.GetAssemblyName(file);

            if (name.Name == typeof(PermissionNodeReaderTests).Assembly.GetName().Name)
                continue;

            Type[] types;

            try
            {
                types = Assembly.Load(name).GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = [.. ex.Types.OfType<Type>()];
            }

            foreach (var type in types.Where(x => x.DeclaringType is null))
            {
                if (type == typeof(CorePermissionNodeSource))
                    continue;

                foreach (var method in IlScanner.MethodsOf(type))
                    IlScanner.Scan(method, strings);
            }
        }

        return strings;
    }
}
