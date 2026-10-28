using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins.Mcp;

/// <summary>An argument of a prompt, as <c>prompts/list</c> sends it.</summary>
public sealed record McpPromptArgument(string Name, string? Description, bool Required);

/// <summary>
/// A live prompt as an MCP server offers it: its name, words, arguments (read from text into its
/// arguments' type and checked), the nodes that may get it (any one) and the plugin that brought
/// it. The host's registry (<see cref="IMcpPrompts"/>) and the test kit both get prompts through it.
/// </summary>
public sealed class McpPromptDefinition
{
    private readonly JsonSerializerOptions _json;
    private readonly JsonObject _schema;

    private McpPromptDefinition(
        IMcpPrompt prompt,
        string owner,
        ImmutableArray<string> nodes,
        JsonSerializerOptions json,
        JsonObject schema
    )
    {
        Prompt = prompt;
        Owner = owner;
        Nodes = nodes;
        _json = json;
        _schema = schema;

        var properties = schema["properties"] as JsonObject ?? [];
        var required = (schema["required"] as JsonArray ?? [])
            .Select(x => x!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        Arguments =
        [
            .. properties.Select(x => new McpPromptArgument(
                x.Key,
                x.Value?["description"]?.GetValue<string>(),
                required.Contains(x.Key)
            )),
        ];
    }

    /// <summary>The plugin that brought it (its key).</summary>
    public string Owner { get; }

    public IMcpPrompt Prompt { get; }

    public string Name => Prompt.Name;

    public string Title => Prompt.Title;

    public string Description => Prompt.Description;

    /// <summary>Its arguments, by their names in camelCase.</summary>
    public ImmutableArray<McpPromptArgument> Arguments { get; }

    /// <summary>The nodes of its <c>[RequiresPermission]</c>: a caller holding any one may get it.</summary>
    public ImmutableArray<string> Nodes { get; }

    /// <summary>
    /// The prompt as an MCP server offers it. Throws <see cref="InvalidOperationException"/>, in
    /// words for its author, for a bad name, no description, no <c>[RequiresPermission]</c>, or an
    /// argument that is not text, a number or true or false.
    /// </summary>
    public static McpPromptDefinition Create(IMcpPrompt prompt, string owner)
    {
        var name = McpContribution.CheckName(prompt.Name, "prompt", prompt.GetType().Name);
        var what = $"The MCP prompt {name}";

        McpContribution.CheckDescription(prompt.Description, what, "what it asks for");

        var nodes = McpContribution.Nodes(prompt.GetType(), what);
        var json = McpToolDefinition.Options();
        var schema = McpToolDefinition.Schema(json, prompt.ArgumentsType, what, "arguments");

        McpContribution.CheckScalars(schema["properties"] as JsonObject ?? [], what);

        return new McpPromptDefinition(prompt, owner, nodes, json, schema);
    }

    /// <summary>Whether <paramref name="caller"/> may get it: the staff member holds one of its <see cref="Nodes"/>.</summary>
    public Task<bool> IsAvailableToAsync(
        McpCaller caller,
        Func<string, CancellationToken, Task<bool>> holds,
        CancellationToken ct
    ) => McpContribution.HoldsAnyAsync(Nodes, holds, ct);

    /// <summary>
    /// Reads <paramref name="arguments"/> (MCP sends each as text) into its arguments' type and
    /// gets its messages. Arguments that don't fit answer <see cref="McpPromptGetStatus.InvalidArguments"/>
    /// with each problem as a sentence. What the prompt throws is thrown.
    /// </summary>
    public async Task<McpPromptGetResult> GetAsync(
        IReadOnlyDictionary<string, string>? arguments,
        McpToolContext ctx,
        CancellationToken ct
    )
    {
        var problems = new List<string>();
        var args = McpContribution.FromText(
            arguments ?? new Dictionary<string, string>(),
            _schema,
            problems
        );
        object? input = null;

        if (problems.Count == 0)
        {
            try
            {
                input = args.Deserialize(Prompt.ArgumentsType, _json);
            }
            catch (JsonException ex)
            {
                problems.Add($"The arguments could not be read: {ex.Message}");
            }
        }

        if (input is null || problems.Count > 0)
            return new McpPromptGetResult(
                McpPromptGetStatus.InvalidArguments,
                null,
                [],
                $"The prompt {Name} was not made. {string.Join(" ", problems.DefaultIfEmpty("The arguments are missing."))}"
            );

        var result = await Prompt.InvokeAsync(input, ctx, ct).ConfigureAwait(false);

        if (result.Error is { } error)
            return new McpPromptGetResult(McpPromptGetStatus.Error, null, [], error);

        return new McpPromptGetResult(
            McpPromptGetStatus.Ok,
            result.Description ?? Description,
            result.Messages
        );
    }
}

/// <summary>How getting a prompt went.</summary>
public enum McpPromptGetStatus
{
    /// <summary>Its messages were made.</summary>
    Ok,

    /// <summary>The arguments do not fit; the message says each problem.</summary>
    InvalidArguments,

    /// <summary>No such prompt for this caller: not live, or not permitted.</summary>
    Unavailable,

    /// <summary>The prompt could not make them (no such room); the message says why.</summary>
    Error,

    /// <summary>It threw; the error is in the server's log, not in the answer.</summary>
    Failed,
}

/// <summary>A prompt's answer as an MCP server sends it, or why it was not made.</summary>
public sealed record McpPromptGetResult(
    McpPromptGetStatus Status,
    string? Description,
    ImmutableArray<McpPromptMessage> Messages,
    string? Message = null
)
{
    public static McpPromptGetResult Unavailable(string name) =>
        new(
            McpPromptGetStatus.Unavailable,
            null,
            [],
            $"There is no prompt \"{name}\" you may get. prompts/list names the ones you may."
        );

    public static McpPromptGetResult Failed(string name) =>
        new(
            McpPromptGetStatus.Failed,
            null,
            [],
            $"The prompt {name} failed on the server. Its error is in the server's log; tell the hotel's staff."
        );
}
