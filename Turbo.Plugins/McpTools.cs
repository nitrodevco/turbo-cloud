using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Approvals;
using Turbo.Contracts.Hotel;
using Turbo.Contracts.Plugins.Mcp;
using Turbo.Plugins.Approvals;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Plugins;

/// <summary>What one plugin brings to MCP: its tools, resources and prompts.</summary>
internal sealed record McpContributions(
    ImmutableArray<McpToolDefinition> Tools,
    ImmutableArray<McpResourceDefinition> Resources,
    ImmutableArray<McpPromptDefinition> Prompts
)
{
    public static McpContributions None { get; } = new([], [], []);

    public bool IsEmpty => Tools.IsEmpty && Resources.IsEmpty && Prompts.IsEmpty;
}

/// <summary>
/// The MCP tools, resources and prompts of the live plugins (<see cref="IMcpTools"/>,
/// <see cref="IMcpResources"/>, <see cref="IMcpPrompts"/>). A plugin's are found in its assembly as
/// it loads (<see cref="Build"/>), listed once it has started, and taken away as it starts to stop.
/// Who may call, read or get each is asked of the hotel's permissions every time.
/// </summary>
internal sealed class McpTools(IServiceProvider host, ILogger<McpTools> logger)
    : IMcpTools,
        IMcpResources,
        IMcpPrompts
{
    private readonly ConcurrentDictionary<string, McpContributions> _live = new(
        StringComparer.OrdinalIgnoreCase
    );

    public ImmutableArray<McpToolDefinition> All =>
        [.. Ordered.SelectMany(x => x.Tools.OrderBy(t => t.Name, StringComparer.Ordinal))];

    ImmutableArray<McpResourceDefinition> IMcpResources.All => Resources;

    ImmutableArray<McpPromptDefinition> IMcpPrompts.All => Prompts;

    /// <summary>Every live resource and template, by plugin key then name.</summary>
    public ImmutableArray<McpResourceDefinition> Resources =>
        [.. Ordered.SelectMany(x => x.Resources.OrderBy(t => t.Name, StringComparer.Ordinal))];

    /// <summary>Every live prompt, by plugin key then name.</summary>
    public ImmutableArray<McpPromptDefinition> Prompts =>
        [.. Ordered.SelectMany(x => x.Prompts.OrderBy(t => t.Name, StringComparer.Ordinal))];

    private IEnumerable<McpContributions> Ordered =>
        _live.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => x.Value);

    public async Task<ImmutableArray<McpToolDefinition>> AvailableToAsync(
        McpCaller caller,
        CancellationToken ct
    )
    {
        var available = ImmutableArray.CreateBuilder<McpToolDefinition>();

        foreach (var tool in All)
            if (await tool.IsAvailableToAsync(caller, Holds(caller), ct).ConfigureAwait(false))
                available.Add(tool);

        return available.ToImmutable();
    }

    async Task<ImmutableArray<McpResourceDefinition>> IMcpResources.AvailableToAsync(
        McpCaller caller,
        CancellationToken ct
    )
    {
        var available = ImmutableArray.CreateBuilder<McpResourceDefinition>();

        foreach (var resource in Resources)
            if (await resource.IsAvailableToAsync(caller, Holds(caller), ct).ConfigureAwait(false))
                available.Add(resource);

        return available.ToImmutable();
    }

    async Task<ImmutableArray<McpPromptDefinition>> IMcpPrompts.AvailableToAsync(
        McpCaller caller,
        CancellationToken ct
    )
    {
        var available = ImmutableArray.CreateBuilder<McpPromptDefinition>();

        foreach (var prompt in Prompts)
            if (await prompt.IsAvailableToAsync(caller, Holds(caller), ct).ConfigureAwait(false))
                available.Add(prompt);

        return available.ToImmutable();
    }

    public async Task<McpToolCallResult> CallAsync(
        string name,
        JsonElement? arguments,
        McpCaller caller,
        CancellationToken ct
    )
    {
        var holds = Holds(caller);

        if (
            All.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal))
                is not { } tool
            || !await tool.IsAvailableToAsync(caller, holds, ct).ConfigureAwait(false)
        )
            return McpToolCallResult.Unavailable(name);

        try
        {
            if (tool.RequiresApproval)
                return await RequestApprovalAsync(tool, arguments, caller, ct)
                    .ConfigureAwait(false);

            return await tool.CallAsync(arguments, new McpToolContext(caller, holds), ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "The plugin {Key}'s MCP tool {Tool} failed for player {Player}",
                tool.Owner,
                tool.Name,
                caller.Player.Value
            );

            return McpToolCallResult.Failed(tool.Name);
        }
    }

    public async Task<McpResourceReadResult> ReadAsync(
        string uri,
        McpCaller caller,
        CancellationToken ct
    )
    {
        var holds = Holds(caller);
        var all = Resources;

        // Its own address first, then the first template that makes it.
        if (
            (
                all.FirstOrDefault(x => !x.IsTemplate && x.Matches(uri))
                ?? all.FirstOrDefault(x => x.IsTemplate && x.Matches(uri))
            )
                is not { } resource
            || !await resource.IsAvailableToAsync(caller, holds, ct).ConfigureAwait(false)
        )
            return McpResourceReadResult.NotFound(uri);

        try
        {
            return await resource
                .ReadAsync(uri, new McpToolContext(caller, holds), ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "The plugin {Key}'s MCP resource {Resource} failed reading {Uri} for player {Player}",
                resource.Owner,
                resource.Name,
                uri,
                caller.Player.Value
            );

            return McpResourceReadResult.Failed(uri);
        }
    }

    public async Task<McpPromptGetResult> GetAsync(
        string name,
        IReadOnlyDictionary<string, string>? arguments,
        McpCaller caller,
        CancellationToken ct
    )
    {
        var holds = Holds(caller);

        if (
            Prompts.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal))
                is not { } prompt
            || !await prompt.IsAvailableToAsync(caller, holds, ct).ConfigureAwait(false)
        )
            return McpPromptGetResult.Unavailable(name);

        try
        {
            return await prompt
                .GetAsync(arguments, new McpToolContext(caller, holds), ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "The plugin {Key}'s MCP prompt {Prompt} failed for player {Player}",
                prompt.Owner,
                prompt.Name,
                caller.Player.Value
            );

            return McpPromptGetResult.Failed(prompt.Name);
        }
    }

    /// <summary>
    /// Runs an approved call (<see cref="McpToolApprovalHandler"/>): the tool, still live, for the
    /// staff member who asked, who must still hold one of its nodes.
    /// </summary>
    internal async Task<McpToolCallResult> RunApprovedAsync(
        McpToolApproval call,
        McpCaller caller,
        CancellationToken ct
    )
    {
        var holds = Holds(caller);

        if (
            All.FirstOrDefault(x => string.Equals(x.Name, call.Tool, StringComparison.Ordinal))
                is not { RequiresApproval: true } tool
            || !await tool.IsAvailableToAsync(caller, holds, ct).ConfigureAwait(false)
        )
            return McpToolCallResult.Unavailable(call.Tool);

        if (tool.CheckArguments(call.Arguments, out var input) is { } invalid)
            return invalid;

        return await tool.RunAsync(input!, new McpToolContext(caller, holds), ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The tool needs a person's yes: its checked arguments are saved as an approval of
    /// <see cref="McpToolApproval.KIND"/> asked for the caller, and the assistant is told the id.
    /// </summary>
    private async Task<McpToolCallResult> RequestApprovalAsync(
        McpToolDefinition tool,
        JsonElement? arguments,
        McpCaller caller,
        CancellationToken ct
    )
    {
        if (tool.CheckArguments(arguments, out var input) is { } invalid)
            return invalid;

        var args = tool.WriteArguments(input!);
        var summary = $"Run the MCP tool {tool.Title} ({tool.Name}) with {args.GetRawText()}";

        if (summary.Length > ApprovalRequest.SUMMARY_MAX_LENGTH)
            summary = summary[..(ApprovalRequest.SUMMARY_MAX_LENGTH - 3)] + "...";

        var approval = await host.GetRequiredService<HotelApprovals>()
            .RequestAsync(
                new ApprovalRequest
                {
                    Kind = McpToolApproval.KIND,
                    Summary = summary,
                    Arguments = new McpToolApproval(tool.Name, args),
                    Requester = ApprovalRequester.Token(caller.Player, "MCP"),
                    Node = tool.Tool.ApprovalNode,
                },
                tool.Owner,
                ct
            )
            .ConfigureAwait(false);

        return new McpToolCallResult(
            McpToolCallStatus.PendingApproval,
            [
                $"{tool.Name} was not run yet: it needs a person's approval. Pending approval {approval.Id}; it runs when a staff member approves it in the admin panel, and is dropped if nobody does by {approval.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC. Don't call it again for the same thing.",
            ],
            JsonSerializer.SerializeToElement(
                new
                {
                    approvalId = approval.Id,
                    state = "pending",
                    expiresAt = approval.ExpiresAtUtc,
                }
            )
        );
    }

    /// <summary>
    /// The tools, resources and prompts <paramref name="asm"/> declares (public
    /// <see cref="IMcpTool"/>s, <see cref="IMcpResource"/>s and <see cref="IMcpPrompt"/>s), each
    /// built with <paramref name="services"/>. Throws <see cref="InvalidOperationException"/> for
    /// one MCP can't offer (<see cref="McpToolDefinition.Create"/> and its siblings), a name (a
    /// resource's address) used twice in the plugin, or one another live plugin has. One for
    /// development servers only (<see cref="IMcpTool.DevelopmentOnly"/>) is left out on any other.
    /// </summary>
    public McpContributions Build(string key, Assembly asm, IServiceProvider services)
    {
        var development = host.GetService<IHostEnvironment>()?.IsDevelopment() == true;
        var others = _live
            .Where(x => !string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var tools = Scan<IMcpTool, McpToolDefinition>(
            new(key, asm, services, development, "tool"),
            x => x.Name,
            x => x.DevelopmentOnly,
            x => McpToolDefinition.Create(x, key),
            x => x.Name,
            name => others.FirstOrDefault(x => x.Value.Tools.Any(t => t.Name == name)).Key,
            (name, other) =>
                $"has the name of the plugin {other}'s: an assistant calls a tool by name, so each is one plugin's. Name it {key}_{name}."
        );
        var resources = Scan<IMcpResource, McpResourceDefinition>(
            new(key, asm, services, development, "resource"),
            x => x.Name,
            x => x.DevelopmentOnly,
            x => McpResourceDefinition.Create(x, key),
            x => x.Uri,
            uri => others.FirstOrDefault(x => x.Value.Resources.Any(t => t.Uri == uri)).Key,
            (_, other) =>
                $"has the address of the plugin {other}'s: an assistant reads a resource by its address, so each is one plugin's."
        );
        var prompts = Scan<IMcpPrompt, McpPromptDefinition>(
            new(key, asm, services, development, "prompt"),
            x => x.Name,
            x => x.DevelopmentOnly,
            x => McpPromptDefinition.Create(x, key),
            x => x.Name,
            name => others.FirstOrDefault(x => x.Value.Prompts.Any(t => t.Name == name)).Key,
            (name, other) =>
                $"has the name of the plugin {other}'s: an assistant gets a prompt by name, so each is one plugin's. Name it {key}_{name}."
        );

        return new McpContributions(tools, resources, prompts);
    }

    /// <summary>Lists the plugin's tools, resources and prompts; disposing the result takes them away.</summary>
    public IDisposable Live(string key, McpContributions contributions)
    {
        _live[key] = contributions;

        return new Removal(this, key, contributions);
    }

    /// <summary>The plugin is reloading, stopping, turned off, failed or gone: it has none.</summary>
    public void Off(string key) => _live.TryRemove(key, out _);

    private ImmutableArray<TDefinition> Scan<TContract, TDefinition>(
        ScanOf of,
        Func<TContract, string> nameOf,
        Func<TContract, bool> developmentOnly,
        Func<TContract, TDefinition> create,
        Func<TDefinition, string> identity,
        Func<string, string?> takenBy,
        Func<string, string, string> taken
    )
    {
        var found = new List<TDefinition>();

        foreach (var concrete in AssemblyExplorer.FindAssignees(of.Assembly, typeof(TContract)))
        {
            var instance = (TContract)ActivatorUtilities.CreateInstance(of.Services, concrete);
            TDefinition definition;

            if (developmentOnly(instance) && !of.Development)
            {
                logger.LogDebug(
                    "The plugin {Key}'s MCP {Kind} {Name} is for development servers only: not registered",
                    of.Key,
                    of.Kind,
                    nameOf(instance)
                );

                continue;
            }

            try
            {
                definition = create(instance);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"The plugin {of.Key}: {ex.Message}", ex);
            }

            var id = identity(definition);

            if (found.Any(x => identity(x) == id))
                throw new InvalidOperationException(
                    of.Kind == "resource"
                        ? $"The plugin {of.Key} has two MCP resources at {id}."
                        : $"The plugin {of.Key} has two MCP {of.Kind}s named {id}."
                );

            if (takenBy(id) is { } other)
                throw new InvalidOperationException(
                    $"The plugin {of.Key}'s MCP {of.Kind} {id} {taken(id, other)}"
                );

            found.Add(definition);
        }

        return [.. found];
    }

    private Func<string, CancellationToken, Task<bool>> Holds(McpCaller caller)
    {
        var hotel = host.GetRequiredService<IHotel>();

        return (node, ct) => hotel.Players.HasPermissionAsync(caller.Player, node, ct);
    }

    private sealed record ScanOf(
        string Key,
        Assembly Assembly,
        IServiceProvider Services,
        bool Development,
        string Kind
    );

    private sealed class Removal(McpTools owner, string key, McpContributions contributions)
        : IDisposable
    {
        public void Dispose()
        {
            // Only the version it listed: a newer one may already have replaced it.
            if (owner._live.TryGetValue(key, out var live) && ReferenceEquals(live, contributions))
                owner._live.TryRemove(key, out _);
        }
    }
}
