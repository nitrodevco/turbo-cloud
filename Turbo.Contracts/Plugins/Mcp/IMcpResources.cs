using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins.Mcp;

/// <summary>
/// The MCP resources of the live plugins, for the plugin that serves them to assistants. Live and
/// gone with their plugin as tools are (<see cref="IMcpTools"/>); who may read each is decided here.
/// </summary>
public interface IMcpResources
{
    /// <summary>Every live resource and template, by plugin key then name.</summary>
    ImmutableArray<McpResourceDefinition> All { get; }

    /// <summary>The resources and templates <paramref name="caller"/> may read: the ones it holds a node of.</summary>
    Task<ImmutableArray<McpResourceDefinition>> AvailableToAsync(
        McpCaller caller,
        CancellationToken ct
    );

    /// <summary>
    /// Reads the resource at <paramref name="uri"/> for <paramref name="caller"/>: the live
    /// resource whose URI it is, else the first template that makes it. No such resource, one the
    /// caller may not read and one that says nothing is there answer
    /// <see cref="McpResourceReadStatus.NotFound"/>; one that throws answers
    /// <see cref="McpResourceReadStatus.Failed"/> and is logged with its plugin's key.
    /// </summary>
    Task<McpResourceReadResult> ReadAsync(string uri, McpCaller caller, CancellationToken ct);
}

/// <summary>
/// The MCP prompts of the live plugins, for the plugin that serves them to assistants. Live and
/// gone with their plugin as tools are (<see cref="IMcpTools"/>); who may get each is decided here.
/// </summary>
public interface IMcpPrompts
{
    /// <summary>Every live prompt, by plugin key then name.</summary>
    ImmutableArray<McpPromptDefinition> All { get; }

    /// <summary>The prompts <paramref name="caller"/> may get: the ones it holds a node of.</summary>
    Task<ImmutableArray<McpPromptDefinition>> AvailableToAsync(
        McpCaller caller,
        CancellationToken ct
    );

    /// <summary>
    /// Gets a prompt for <paramref name="caller"/> with its arguments as text. One that is not live
    /// and one the caller may not get answer <see cref="McpPromptGetStatus.Unavailable"/> alike;
    /// one that throws answers <see cref="McpPromptGetStatus.Failed"/> and is logged with its plugin's key.
    /// </summary>
    Task<McpPromptGetResult> GetAsync(
        string name,
        IReadOnlyDictionary<string, string>? arguments,
        McpCaller caller,
        CancellationToken ct
    );
}
