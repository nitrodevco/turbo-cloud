using System;
using System.Collections.Frozen;
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
    /// <c>TurboCommandTreeMessage</c> and the suggestion request and answer: the chat commands the
    /// player may use, as data, so a client completes and checks a line as it is typed.
    /// </summary>
    public const string CHAT_COMMANDS = "chat.commands.v2";

    /// <summary>Maximum preceding argument text in a suggestion request.</summary>
    public const int MAX_SUGGEST_CONTEXT = 1024;

    /// <summary>
    /// The longest command name or prefix a suggestion request is read for; the rest is dropped. A
    /// protocol bound against a hostile string, not a tunable.
    /// </summary>
    public const int MAX_SUGGEST_TEXT = 64;

    /// <summary>
    /// The most extensions one request is read for; the rest of a longer one is ignored. A
    /// protocol bound against a hostile count, not a tunable.
    /// </summary>
    public const int MAX_REQUESTED = 32;

    /// <summary>Every extension this server speaks, at the highest version it speaks.</summary>
    public static readonly FrozenDictionary<string, int> SUPPORTED = new Dictionary<string, int>
    {
        [PERMISSION_NODES] = 1,
        [CHAT_COMMANDS] = 1,
    }.ToFrozenDictionary(StringComparer.Ordinal);

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
