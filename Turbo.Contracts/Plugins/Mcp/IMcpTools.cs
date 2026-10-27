using System.Collections.Immutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins.Mcp;

/// <summary>
/// The MCP tools of the live plugins, for the plugin that serves them to assistants (an MCP
/// server). A plugin's tools are listed once it has started and gone the moment it starts to
/// unload; who may see and call each is decided here, the same for every server.
/// </summary>
public interface IMcpTools
{
    /// <summary>Every live tool, by plugin key then name.</summary>
    ImmutableArray<McpToolDefinition> All { get; }

    /// <summary>The tools <paramref name="caller"/> may call: the ones it holds a node of, read-only ones only for read-only access.</summary>
    Task<ImmutableArray<McpToolDefinition>> AvailableToAsync(
        McpCaller caller,
        CancellationToken ct
    );

    /// <summary>
    /// Calls the tool for <paramref name="caller"/>. A tool that is not live, or one the caller may
    /// not call, answers <see cref="McpToolCallStatus.Unavailable"/> alike, so a caller can't tell
    /// a tool it may not see from none; one that throws answers <see cref="McpToolCallStatus.Failed"/>
    /// and is logged with its plugin's key.
    /// </summary>
    Task<McpToolCallResult> CallAsync(
        string name,
        JsonElement? arguments,
        McpCaller caller,
        CancellationToken ct
    );
}

/// <summary>How a call went.</summary>
public enum McpToolCallStatus
{
    /// <summary>It ran and answered.</summary>
    Ok,

    /// <summary>It ran and could not do it (<see cref="McpToolResult.Error"/>); the text says why.</summary>
    Error,

    /// <summary>It did not run: the arguments do not fit its schema; the text says each problem.</summary>
    InvalidArguments,

    /// <summary>No such tool for this caller: not live, not permitted, or not read-only for read-only access.</summary>
    Unavailable,

    /// <summary>It threw; the error is in the server's log, not in the answer.</summary>
    Failed,
}

/// <summary>
/// A call's answer as an MCP server sends it: its text blocks (a summary, the note on player text,
/// the data's JSON, or the error), its structured content, and whether it is an error.
/// </summary>
public sealed record McpToolCallResult(
    McpToolCallStatus Status,
    ImmutableArray<string> Text,
    JsonElement? Structured = null
)
{
    /// <summary>MCP's <c>isError</c>: anything but <see cref="McpToolCallStatus.Ok"/>.</summary>
    public bool IsError => Status != McpToolCallStatus.Ok;

    public static McpToolCallResult Unavailable(string name) =>
        new(
            McpToolCallStatus.Unavailable,
            [$"There is no tool \"{name}\" you may call. tools/list names the ones you may."]
        );

    public static McpToolCallResult Failed(string name) =>
        new(
            McpToolCallStatus.Failed,
            [
                $"{name} failed on the server. Its error is in the server's log; tell the hotel's staff.",
            ]
        );
}
