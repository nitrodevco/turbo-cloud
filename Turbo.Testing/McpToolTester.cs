using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Mcp;
using Turbo.Primitives.Players;

namespace Turbo.Testing;

/// <summary>
/// Calls a plugin's MCP tools as a hotel's MCP server does (<c>docs/plugin-mcp-tools.md</c>): the
/// same schemas, argument checks, permission and read-only rules and answers, without a server.
/// <code>
/// var mcp = new McpToolTester(new FindPlayerTool(hotel));
/// var call = await mcp.As(staff, "plugin.mcp.tool.find_player").CallAsync("find_player", new { name = "al" });
/// call.AssertOk().Data&lt;FindPlayerOutput&gt;().Players.Should()...;
/// </code>
/// A tool's own construction error (no <c>[RequiresPermission]</c>, a bad name) throws as the
/// hotel's load would refuse it.
/// </summary>
public sealed class McpToolTester
{
    private static readonly JsonSerializerOptions ARGUMENTS = new(JsonSerializerDefaults.Web);

    private readonly ImmutableArray<McpToolDefinition> _tools;

    public McpToolTester(params IMcpTool[] tools)
    {
        _tools = [.. tools.Select(x => McpToolDefinition.Create(x, "test"))];
    }

    /// <summary>The tools as the hotel lists them: schemas, hints and nodes.</summary>
    public ImmutableArray<McpToolDefinition> Tools => _tools;

    /// <summary>A staff member holding exactly <paramref name="nodes"/> (<c>*</c> holds them all).</summary>
    public McpTestCaller As(PlayerId player, params string[] nodes)
    {
        var held = new HashSet<string>(nodes, StringComparer.Ordinal);

        return new McpTestCaller(
            _tools,
            new McpCaller(player),
            node => held.Contains(node) || held.Contains("*")
        );
    }

    /// <summary>A fake hotel's player, holding its <see cref="FakePlayer.Permissions"/> as they are at each call.</summary>
    public McpTestCaller As(FakePlayer player) =>
        new(
            _tools,
            new McpCaller(player.Id),
            node => player.Permissions.Contains(node) || player.Permissions.Contains("*")
        );

    /// <summary>
    /// The tools as the hotel's <see cref="IMcpTools"/>, each caller's nodes asked of
    /// <paramref name="hotel"/>: what an MCP server plugin's tests serve.
    /// </summary>
    public IMcpTools Registry(FakeHotel hotel) => new Live(_tools, hotel);

    internal static JsonElement? Arguments(object? arguments) =>
        arguments switch
        {
            null => null,
            JsonElement element => element,
            string json => JsonSerializer.Deserialize<JsonElement>(json),
            _ => JsonSerializer.SerializeToElement(arguments, arguments.GetType(), ARGUMENTS),
        };

    private sealed class Live(ImmutableArray<McpToolDefinition> tools, FakeHotel hotel) : IMcpTools
    {
        public ImmutableArray<McpToolDefinition> All => tools;

        public async Task<ImmutableArray<McpToolDefinition>> AvailableToAsync(
            McpCaller caller,
            CancellationToken ct
        )
        {
            var available = ImmutableArray.CreateBuilder<McpToolDefinition>();

            foreach (var tool in tools)
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
            var tool = tools.FirstOrDefault(x => x.Name == name);

            if (
                tool is null
                || !await tool.IsAvailableToAsync(caller, Holds(caller), ct).ConfigureAwait(false)
            )
                return McpToolCallResult.Unavailable(name);

            return await tool.CallAsync(arguments, new McpToolContext(caller, Holds(caller)), ct)
                .ConfigureAwait(false);
        }

        private Func<string, CancellationToken, Task<bool>> Holds(McpCaller caller) =>
            (node, ct) => hotel.Players.HasPermissionAsync(caller.Player, node, ct);
    }
}

/// <summary>An assistant acting for one staff member: what it is offered and what its calls answer.</summary>
public sealed class McpTestCaller
{
    private readonly ImmutableArray<McpToolDefinition> _tools;
    private readonly McpCaller _caller;
    private readonly Func<string, bool> _holds;

    internal McpTestCaller(
        ImmutableArray<McpToolDefinition> tools,
        McpCaller caller,
        Func<string, bool> holds
    )
    {
        _tools = tools;
        _caller = caller;
        _holds = holds;
    }

    /// <summary>The same staff member through read-only access: only read-only tools.</summary>
    public McpTestCaller ReadOnly() => new(_tools, _caller with { ReadOnly = true }, _holds);

    /// <summary>The names of the tools it is offered (<c>tools/list</c>).</summary>
    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
    {
        var names = new List<string>();

        foreach (var tool in _tools)
            if (await tool.IsAvailableToAsync(_caller, HoldsAsync, ct).ConfigureAwait(false))
                names.Add(tool.Name);

        return names;
    }

    /// <summary>
    /// Calls a tool (<c>tools/call</c>) with <paramref name="arguments"/>: an object written as
    /// JSON in camelCase (<c>new { name = "al" }</c>), a JSON string, or a <see cref="JsonElement"/>.
    /// What the tool throws is thrown.
    /// </summary>
    public async Task<McpTestCall> CallAsync(
        string name,
        object? arguments = null,
        CancellationToken ct = default
    )
    {
        var tool = _tools.FirstOrDefault(x => x.Name == name);

        if (
            tool is null
            || !await tool.IsAvailableToAsync(_caller, HoldsAsync, ct).ConfigureAwait(false)
        )
            return new McpTestCall(name, McpToolCallResult.Unavailable(name));

        var result = await tool.CallAsync(
                McpToolTester.Arguments(arguments),
                new McpToolContext(_caller, HoldsAsync),
                ct
            )
            .ConfigureAwait(false);

        return new McpTestCall(name, result);
    }

    private Task<bool> HoldsAsync(string node, CancellationToken ct) =>
        Task.FromResult(_holds(node));
}

/// <summary>A call's answer, with assertions that say what came back instead.</summary>
public sealed class McpTestCall(string tool, McpToolCallResult result)
{
    private static readonly JsonSerializerOptions DATA = new(JsonSerializerDefaults.Web);

    public McpToolCallResult Result => result;

    public McpToolCallStatus Status => result.Status;

    /// <summary>Every text block, one per line.</summary>
    public string Text => string.Join("\n", result.Text);

    /// <summary>The structured content as <typeparamref name="T"/>.</summary>
    public T Data<T>() =>
        result.Structured is { } structured
            ? structured.Deserialize<T>(DATA)
                ?? throw new HotelAssertionException($"{tool} answered null data.")
            : throw new HotelAssertionException($"{tool} answered no data: {Text}");

    public McpTestCall AssertOk() =>
        result.Status == McpToolCallStatus.Ok
            ? this
            : throw new HotelAssertionException($"{tool} answered {result.Status}: {Text}");

    /// <summary>The tool ran and refused (<see cref="McpToolResult.Error"/>), its text holding <paramref name="contains"/>.</summary>
    public McpTestCall AssertError(string contains) => Expect(McpToolCallStatus.Error, contains);

    /// <summary>The arguments were refused before it ran, the text holding <paramref name="contains"/>.</summary>
    public McpTestCall AssertInvalid(string contains) =>
        Expect(McpToolCallStatus.InvalidArguments, contains);

    /// <summary>The caller may not call it (no node, or read-only access to a tool that changes things).</summary>
    public McpTestCall AssertUnavailable() => Expect(McpToolCallStatus.Unavailable, string.Empty);

    private McpTestCall Expect(McpToolCallStatus status, string contains) =>
        result.Status == status && Text.Contains(contains, StringComparison.Ordinal)
            ? this
            : throw new HotelAssertionException(
                $"{tool} was expected to answer {status} with \"{contains}\"; it answered {result.Status}: {Text}"
            );
}
