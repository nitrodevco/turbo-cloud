using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins.Mcp;

/// <summary>
/// A prompt a staff member picks in their assistant (MCP's <c>prompts/get</c>,
/// <c>docs/plugin-mcp-tools.md</c>): "investigate wired in room 42" filled into the messages that
/// start the conversation. A public class deriving from <see cref="McpPrompt{TArgs}"/> in a plugin's
/// assembly is found as the plugin loads and taken away as it unloads, as a tool is. Every prompt
/// needs <c>[RequiresPermission]</c>; a prompt changes nothing, so read-only access is offered it.
/// </summary>
public interface IMcpPrompt
{
    /// <summary>What it is picked by: letters, digits, <c>_</c>, <c>-</c> and <c>.</c>, at most 64.</summary>
    string Name { get; }

    /// <summary>What people read in the assistant's prompt list.</summary>
    string Title { get; }

    /// <summary>What it asks the assistant to do.</summary>
    string Description { get; }

    /// <summary>It exists only on a development server, as <see cref="IMcpTool.DevelopmentOnly"/>.</summary>
    bool DevelopmentOnly { get; }

    /// <summary>The arguments' type: a record whose properties are the arguments (text, numbers, true or false).</summary>
    Type ArgumentsType { get; }

    /// <summary>The messages, with arguments of <see cref="ArgumentsType"/> already checked.</summary>
    Task<McpPromptResult> InvokeAsync(object arguments, McpToolContext ctx, CancellationToken ct);
}

/// <summary>
/// A prompt taking <typeparamref name="TArgs"/> (a record: each property an argument, sent by the
/// assistant as text and read as the property's type; a parameter with a default or a nullable one
/// is optional; <c>[Description]</c> describes it). <see cref="McpNoInput"/> for one that takes none.
/// </summary>
public abstract class McpPrompt<TArgs> : IMcpPrompt
    where TArgs : notnull
{
    public abstract string Name { get; }

    public virtual string Title => Name;

    public abstract string Description { get; }

    public virtual bool DevelopmentOnly => false;

    public Type ArgumentsType => typeof(TArgs);

    public abstract Task<McpPromptResult> HandleAsync(
        TArgs arguments,
        McpToolContext ctx,
        CancellationToken ct
    );

    Task<McpPromptResult> IMcpPrompt.InvokeAsync(
        object arguments,
        McpToolContext ctx,
        CancellationToken ct
    ) => HandleAsync((TArgs)arguments, ctx, ct);
}

/// <summary>Who a prompt's message is from.</summary>
public enum McpPromptRole
{
    User,
    Assistant,
}

/// <summary>One message of a prompt: text, or a link to a resource the assistant may read.</summary>
public sealed record McpPromptMessage(
    McpPromptRole Role,
    string? Text = null,
    McpResourceLink? Link = null
)
{
    /// <summary>The staff member's words.</summary>
    public static McpPromptMessage User(string text) => new(McpPromptRole.User, text);

    /// <summary>The assistant's words, as if it had said them.</summary>
    public static McpPromptMessage Assistant(string text) => new(McpPromptRole.Assistant, text);

    /// <summary>A resource to read, from the staff member.</summary>
    public static McpPromptMessage LinkTo(McpResourceLink link) =>
        new(McpPromptRole.User, Link: link);
}

/// <summary>What a prompt answers: its messages and a line saying what they are for, or why it can't.</summary>
public sealed class McpPromptResult
{
    private McpPromptResult(
        ImmutableArray<McpPromptMessage> messages,
        string? description,
        string? error
    )
    {
        Messages = messages;
        Description = description;
        Error = error;
    }

    public ImmutableArray<McpPromptMessage> Messages { get; }

    public string? Description { get; }

    /// <summary>Why it could not be made (no such room); null when it was.</summary>
    public string? Error { get; }

    public static McpPromptResult Of(string? description, params McpPromptMessage[] messages) =>
        new([.. messages], description, null);

    public static McpPromptResult Of(string? description, IEnumerable<McpPromptMessage> messages) =>
        new([.. messages], description, null);

    /// <summary>It could not be made: a sentence the assistant can act on.</summary>
    public static McpPromptResult Failed(string message) => new([], null, message);
}
