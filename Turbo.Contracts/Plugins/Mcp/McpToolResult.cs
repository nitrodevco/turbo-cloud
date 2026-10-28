using System.Collections.Immutable;

namespace Turbo.Contracts.Plugins.Mcp;

/// <summary>
/// What a tool answers: text, data (a record, sent as structured content and as its JSON), or an
/// error the assistant reads and acts on. Write an error as a sentence that says what to do next
/// ("No player is called Alise. find_player takes the start of a name."), not as a code.
/// </summary>
public sealed class McpToolResult
{
    private McpToolResult(
        bool isError,
        string? text,
        object? value,
        ImmutableArray<McpResourceLink> links = default
    )
    {
        IsError = isError;
        Message = text;
        Value = value;
        Links = links.IsDefault ? [] : links;
    }

    /// <summary>The tool could not do what was asked; <see cref="Message"/> says why.</summary>
    public bool IsError { get; }

    /// <summary>A text answer, the sentence before the data, or the error.</summary>
    public string? Message { get; }

    /// <summary>The data answered, sent as the call's structured content; null for text.</summary>
    public object? Value { get; }

    /// <summary>Resources the answer points to, sent after its text as <c>resource_link</c> content.</summary>
    public ImmutableArray<McpResourceLink> Links { get; }

    /// <summary>An answer in words.</summary>
    public static McpToolResult Text(string text) => new(false, text, null);

    /// <summary>An answer as data: an object (a record or class), never a list or a value on its own.</summary>
    public static McpToolResult Data(object value, string? summary = null) =>
        new(false, summary, value);

    /// <summary>The tool could not do it: a sentence the assistant can act on.</summary>
    public static McpToolResult Error(string message) => new(true, message, null);

    /// <summary>The same answer pointing to <paramref name="links"/> as well (a room's resource, a player's).</summary>
    public McpToolResult WithLinks(params McpResourceLink[] links) =>
        new(IsError, Message, Value, [.. Links, .. links]);
}
