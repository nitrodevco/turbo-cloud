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

/// <summary>
/// The MCP tools of the live plugins (<see cref="IMcpTools"/>). A plugin's tools are found in its
/// assembly as it loads (<see cref="Build"/>), listed once it has started, and taken away as it
/// starts to stop. Who may call a tool is asked of the hotel's permissions on every call.
/// </summary>
internal sealed class McpTools(IServiceProvider host, ILogger<McpTools> logger) : IMcpTools
{
    private readonly ConcurrentDictionary<string, ImmutableArray<McpToolDefinition>> _live = new(
        StringComparer.OrdinalIgnoreCase
    );

    public ImmutableArray<McpToolDefinition> All =>
        [
            .. _live
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .SelectMany(x => x.Value.OrderBy(t => t.Name, StringComparer.Ordinal)),
        ];

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
    /// The tools <paramref name="asm"/> declares (public <see cref="IMcpTool"/>s), each built with
    /// <paramref name="services"/>. Throws <see cref="InvalidOperationException"/> for a tool MCP
    /// can't offer (<see cref="McpToolDefinition.Create"/>), a name used twice in the plugin, or a
    /// name another live plugin's tool has. A tool for development servers only
    /// (<see cref="IMcpTool.DevelopmentOnly"/>) is left out on any other.
    /// </summary>
    public ImmutableArray<McpToolDefinition> Build(
        string key,
        Assembly asm,
        IServiceProvider services
    )
    {
        var tools = new List<McpToolDefinition>();
        var development = host.GetService<IHostEnvironment>()?.IsDevelopment() == true;

        foreach (var concrete in AssemblyExplorer.FindAssignees(asm, typeof(IMcpTool)))
        {
            var instance = (IMcpTool)ActivatorUtilities.CreateInstance(services, concrete);
            McpToolDefinition tool;

            if (instance.DevelopmentOnly && !development)
            {
                logger.LogDebug(
                    "The plugin {Key}'s MCP tool {Tool} is for development servers only: not registered",
                    key,
                    instance.Name
                );

                continue;
            }

            try
            {
                tool = McpToolDefinition.Create(instance, key);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"The plugin {key}: {ex.Message}", ex);
            }

            if (tools.Any(x => x.Name == tool.Name))
                throw new InvalidOperationException(
                    $"The plugin {key} has two MCP tools named {tool.Name}."
                );

            if (
                _live.FirstOrDefault(x =>
                    !string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)
                    && x.Value.Any(t => t.Name == tool.Name)
                ) is
                { Key: { } other }
            )
                throw new InvalidOperationException(
                    $"The plugin {key}'s MCP tool {tool.Name} has the name of the plugin {other}'s: an assistant calls a tool by name, so each is one plugin's. Name it {key}_{tool.Name}."
                );

            tools.Add(tool);
        }

        return [.. tools];
    }

    /// <summary>Lists the plugin's tools; disposing the result takes them away.</summary>
    public IDisposable Live(string key, ImmutableArray<McpToolDefinition> tools)
    {
        _live[key] = tools;

        return new Removal(this, key, tools);
    }

    /// <summary>The plugin is reloading, stopping, turned off, failed or gone: it has no tools.</summary>
    public void Off(string key) => _live.TryRemove(key, out _);

    private Func<string, CancellationToken, Task<bool>> Holds(McpCaller caller)
    {
        var hotel = host.GetRequiredService<IHotel>();

        return (node, ct) => hotel.Players.HasPermissionAsync(caller.Player, node, ct);
    }

    private sealed class Removal(
        McpTools owner,
        string key,
        ImmutableArray<McpToolDefinition> tools
    ) : IDisposable
    {
        public void Dispose()
        {
            // Only the version it listed: a newer one may already have replaced it.
            if (owner._live.TryGetValue(key, out var live) && live == tools)
                owner._live.TryRemove(key, out _);
        }
    }
}
