using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Contracts.Plugins.Mcp;

/// <summary>
/// Who a tool runs for: the staff member the assistant acts as, whether the assistant's access is
/// read-only, and a check of the member's other permission nodes, for a tool that shows more (an
/// email, an address, private messages) only to staff holding a node of its own.
/// </summary>
public sealed class McpToolContext(
    McpCaller caller,
    Func<string, CancellationToken, Task<bool>> holds
)
{
    /// <summary>The staff member the assistant acts as.</summary>
    public PlayerId Caller => caller.Player;

    /// <summary>The assistant's access is read-only: only read-only tools run for it.</summary>
    public bool ReadOnly => caller.ReadOnly;

    /// <summary>Whether the staff member holds <paramref name="node"/> now.</summary>
    public Task<bool> HoldsAsync(string node, CancellationToken ct) => holds(node, ct);
}

/// <summary>
/// An assistant's caller: the staff member its access belongs to, and whether that access is
/// read-only (it is offered and may call only read-only tools).
/// </summary>
public sealed record McpCaller(PlayerId Player, bool ReadOnly = false);
