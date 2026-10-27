using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Contracts.Plugins.Mcp;

/// <summary>
/// A tool an AI assistant may call on the hotel through an MCP server plugin
/// (<c>docs/plugin-mcp-tools.md</c>). A public class deriving from <see cref="McpTool{TInput}"/> or
/// <see cref="McpTool{TInput, TOutput}"/> in a plugin's assembly is found as the plugin loads, built
/// with the plugin's services and then the host's, and taken away as it unloads. Every tool needs
/// <c>[RequiresPermission]</c>: the staff member an assistant acts for must hold one of its nodes.
/// </summary>
public interface IMcpTool
{
    /// <summary>What the assistant calls it by: letters, digits, <c>_</c>, <c>-</c> and <c>.</c>, at most 64.</summary>
    string Name { get; }

    /// <summary>A short name people read in the assistant's tool list.</summary>
    string Title { get; }

    /// <summary>What it does and when to use it, written for the assistant.</summary>
    string Description { get; }

    /// <summary>It changes nothing (MCP's <c>readOnlyHint</c>). Only these are offered to a read-only token.</summary>
    bool ReadOnly { get; }

    /// <summary>A change it makes may destroy or replace something (<c>destructiveHint</c>); read for tools that are not read-only.</summary>
    bool Destructive { get; }

    /// <summary>Calling it again with the same arguments changes nothing more (<c>idempotentHint</c>).</summary>
    bool Idempotent { get; }

    /// <summary>It reaches past the hotel, to the web or another service (<c>openWorldHint</c>).</summary>
    bool OpenWorld { get; }

    /// <summary>
    /// A person must approve each call before it runs (<c>docs/approvals.md</c>): over MCP the
    /// call answers "pending approval" with the request's id, and the tool runs, once, when a
    /// staff member holding <see cref="ApprovalNode"/> approves it. Never for a read-only tool.
    /// </summary>
    bool RequiresApproval { get; }

    /// <summary>The node an approver of a call must hold; <c>approvals.decide</c> by default.</summary>
    string ApprovalNode { get; }

    /// <summary>The arguments' type: a record or class, its properties the arguments.</summary>
    Type InputType { get; }

    /// <summary>The structured answer's type (<c>outputSchema</c>); null when it answers in text.</summary>
    Type? OutputType { get; }

    /// <summary>Runs the tool with arguments of <see cref="InputType"/>, already checked against its schema.</summary>
    Task<McpToolResult> InvokeAsync(object input, McpToolContext ctx, CancellationToken ct);
}

/// <summary>
/// A tool taking <typeparamref name="TInput"/> (a record: each property an argument; a parameter
/// with a default is optional; <c>[Description]</c>, <c>[Range]</c> and <c>[StringLength]</c> /
/// <c>[MaxLength]</c> go into its schema) and answering in text or data
/// (<see cref="McpToolResult"/>). <see cref="McpNoInput"/> for a tool that takes none.
/// </summary>
public abstract class McpTool<TInput> : IMcpTool
    where TInput : notnull
{
    public abstract string Name { get; }

    public virtual string Title => Name;

    public abstract string Description { get; }

    public virtual bool ReadOnly => false;

    public virtual bool Destructive => !ReadOnly;

    public virtual bool Idempotent => ReadOnly;

    public virtual bool OpenWorld => false;

    public virtual bool RequiresApproval => false;

    public virtual string ApprovalNode => PermissionNodes.Approvals.DECIDE;

    public Type InputType => typeof(TInput);

    public virtual Type? OutputType => null;

    public abstract Task<McpToolResult> HandleAsync(
        TInput input,
        McpToolContext ctx,
        CancellationToken ct
    );

    Task<McpToolResult> IMcpTool.InvokeAsync(
        object input,
        McpToolContext ctx,
        CancellationToken ct
    ) => HandleAsync((TInput)input, ctx, ct);
}

/// <summary>
/// A tool answering with <typeparamref name="TOutput"/> (a record or class): its schema is the
/// tool's <c>outputSchema</c>, the value its <c>structuredContent</c> and, written as JSON, its text.
/// Answer with <see cref="Ok"/>, or <see cref="McpToolResult.Error"/> when it can't.
/// </summary>
public abstract class McpTool<TInput, TOutput> : McpTool<TInput>
    where TInput : notnull
    where TOutput : class
{
    public sealed override Type? OutputType => typeof(TOutput);

    /// <summary>The answer, with an optional sentence before its JSON.</summary>
    protected static McpToolResult Ok(TOutput value, string? summary = null) =>
        McpToolResult.Data(value, summary);
}

/// <summary>The arguments of a tool that takes none.</summary>
public sealed record McpNoInput;
