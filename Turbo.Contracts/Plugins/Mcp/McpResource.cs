using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins.Mcp;

/// <summary>
/// Something an AI assistant reads by its address through an MCP server plugin
/// (<c>resources/read</c>, <c>docs/plugin-mcp-tools.md</c>): a room's state, a player's profile, a
/// plugin's state. A public class deriving from <see cref="McpResource"/> (one fixed address) or
/// <see cref="McpResourceTemplate{TArgs}"/> (an address with values, <c>turbo://rooms/{roomId}</c>)
/// in a plugin's assembly is found as the plugin loads and taken away as it unloads, as a tool is.
/// Every resource needs <c>[RequiresPermission]</c>; reading changes nothing, so read-only access
/// may read any the staff member may.
/// </summary>
public interface IMcpResource
{
    /// <summary>
    /// The address: a URI (<c>turbo://hotel/status</c>), or for a template a URI template whose
    /// <c>{name}</c> parts are its arguments (<c>turbo://rooms/{roomId}</c>).
    /// </summary>
    string Uri { get; }

    /// <summary>A short name: letters, digits, <c>_</c>, <c>-</c> and <c>.</c>, at most 64.</summary>
    string Name { get; }

    /// <summary>What people read in the assistant's list.</summary>
    string Title { get; }

    /// <summary>What it holds and when to read it, written for the assistant.</summary>
    string Description { get; }

    /// <summary>Its contents' type: <c>application/json</c> unless it says otherwise.</summary>
    string MimeType { get; }

    /// <summary>It exists only on a development server, as <see cref="IMcpTool.DevelopmentOnly"/>.</summary>
    bool DevelopmentOnly { get; }

    /// <summary>The template's arguments' type (each <c>{name}</c> a property); null for a fixed address.</summary>
    Type? ArgumentsType { get; }

    /// <summary>Reads it, with the template's values read into <see cref="ArgumentsType"/> (null for a fixed address).</summary>
    Task<McpResourceResult> InvokeAsync(
        object? arguments,
        McpToolContext ctx,
        CancellationToken ct
    );
}

/// <summary>A resource at one fixed address (<see cref="IMcpResource"/>), listed by <c>resources/list</c>.</summary>
public abstract class McpResource : IMcpResource
{
    public abstract string Uri { get; }

    public abstract string Name { get; }

    public virtual string Title => Name;

    public abstract string Description { get; }

    public virtual string MimeType => "application/json";

    public virtual bool DevelopmentOnly => false;

    public Type? ArgumentsType => null;

    public abstract Task<McpResourceResult> HandleAsync(McpToolContext ctx, CancellationToken ct);

    Task<McpResourceResult> IMcpResource.InvokeAsync(
        object? arguments,
        McpToolContext ctx,
        CancellationToken ct
    ) => HandleAsync(ctx, ct);
}

/// <summary>
/// Resources at addresses made from a URI template (<see cref="IMcpResource"/>), listed by
/// <c>resources/templates/list</c>: <c>turbo://rooms/{roomId}</c> reads <typeparamref name="TArgs"/>
/// (a record whose properties are the template's values: <c>int RoomId</c>) from the address
/// asked for. A value that does not fit its property (<c>turbo://rooms/abc</c>) is no resource.
/// </summary>
public abstract class McpResourceTemplate<TArgs> : IMcpResource
    where TArgs : notnull
{
    /// <summary>The URI template, such as <c>turbo://rooms/{roomId}</c>.</summary>
    public abstract string UriTemplate { get; }

    public string Uri => UriTemplate;

    public abstract string Name { get; }

    public virtual string Title => Name;

    public abstract string Description { get; }

    public virtual string MimeType => "application/json";

    public virtual bool DevelopmentOnly => false;

    public Type? ArgumentsType => typeof(TArgs);

    public abstract Task<McpResourceResult> HandleAsync(
        TArgs arguments,
        McpToolContext ctx,
        CancellationToken ct
    );

    Task<McpResourceResult> IMcpResource.InvokeAsync(
        object? arguments,
        McpToolContext ctx,
        CancellationToken ct
    ) => HandleAsync((TArgs)arguments!, ctx, ct);
}

/// <summary>What reading a resource answers: its text, its data (written as JSON), or that there is none.</summary>
public sealed class McpResourceResult
{
    private McpResourceResult(string? text, object? value, string? missing)
    {
        Text = text;
        Value = value;
        Missing = missing;
    }

    /// <summary>Its contents as text, in the resource's <see cref="IMcpResource.MimeType"/>.</summary>
    public string? Text { get; }

    /// <summary>Its contents as data (a record or class), written as JSON.</summary>
    public object? Value { get; }

    /// <summary>Why nothing is at the address (no such room); null when something is.</summary>
    public string? Missing { get; }

    /// <summary>Its contents as text.</summary>
    public static McpResourceResult Contents(string text) => new(text, null, null);

    /// <summary>Its contents as data: an object (a record or class), written as JSON.</summary>
    public static McpResourceResult Data(object value) => new(null, value, null);

    /// <summary>Nothing is at the address: a sentence the assistant can act on.</summary>
    public static McpResourceResult NotFound(string message) => new(null, null, message);
}
