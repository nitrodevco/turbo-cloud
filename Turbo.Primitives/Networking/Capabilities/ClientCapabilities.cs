using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Turbo.Primitives.Networking.Capabilities;

/// <summary>
/// The protocol extensions Turbo offers a client that asks for them, outside Habbo's protocol.
/// A client opts in by sending <c>TurboClientCapabilitiesMessage</c> after login; one that never
/// asks (Flash, any other client) never receives an extension packet. See
/// <c>docs/client-capabilities.md</c>.
/// </summary>
public static class ClientCapabilities
{
    /// <summary>
    /// <c>TurboPermissionNodesMessage</c>: the client-facing permission nodes the player holds,
    /// sent after the capability answer and again whenever they change.
    /// </summary>
    public const string PERMISSION_NODES = "permission.nodes";

    /// <summary>
    /// The most extensions one request is read for; the rest of a longer one is ignored. A
    /// protocol bound against a hostile count, not a tunable.
    /// </summary>
    public const int MAX_REQUESTED = 32;

    /// <summary>Every extension this server speaks, at the highest version it speaks.</summary>
    public static readonly ImmutableDictionary<string, int> SUPPORTED =
        ImmutableDictionary.CreateRange(
            StringComparer.Ordinal,
            [new KeyValuePair<string, int>(PERMISSION_NODES, 1)]
        );

    /// <summary>
    /// What to accept from a client's request: each extension both sides speak, at the lower of
    /// the two versions, once. Unknown names and versions below 1 are dropped, not refused.
    /// </summary>
    public static ImmutableArray<ClientCapabilitySnapshot> Negotiate(
        IEnumerable<ClientCapabilitySnapshot> requested
    ) =>
        [
            .. requested
                .Where(x => x.Version >= 1 && SUPPORTED.ContainsKey(x.Name))
                .GroupBy(x => x.Name, StringComparer.Ordinal)
                .Select(x => new ClientCapabilitySnapshot
                {
                    Name = x.Key,
                    Version = Math.Min(x.Max(y => y.Version), SUPPORTED[x.Key]),
                })
                .OrderBy(x => x.Name, StringComparer.Ordinal),
        ];
}
