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
/// Calls a plugin's MCP tools, reads its resources and gets its prompts as a hotel's MCP server
/// does (<c>docs/plugin-mcp-tools.md</c>): the same schemas, argument checks, permission and
/// read-only rules and answers, without a server.
/// <code>
/// var mcp = new McpToolTester(new FindPlayerTool(hotel)).WithResources(new RoomResource(hotel));
/// var call = await mcp.As(staff, "plugin.mcp.tool.find_player").CallAsync("find_player", new { name = "al" });
/// call.AssertOk().Data&lt;FindPlayerOutput&gt;().Players.Should()...;
/// (await mcp.As(staff, "*").ReadAsync("turbo://rooms/1")).AssertOk().Data&lt;RoomOutput&gt;();
/// </code>
/// A construction error (no <c>[RequiresPermission]</c>, a bad name or address) throws as the
/// hotel's load would refuse it.
/// </summary>
public sealed class McpToolTester
{
    private static readonly JsonSerializerOptions ARGUMENTS = new(JsonSerializerDefaults.Web);

    private readonly McpTestContributions _all;

    public McpToolTester(params IMcpTool[] tools)
        : this(
            new McpTestContributions(
                [.. tools.Select(x => McpToolDefinition.Create(x, "test"))],
                [],
                []
            )
        ) { }

    private McpToolTester(McpTestContributions all)
    {
        _all = all;
    }

    /// <summary>The tools as the hotel lists them: schemas, hints and nodes.</summary>
    public ImmutableArray<McpToolDefinition> Tools => _all.Tools;

    /// <summary>The resources and templates as the hotel lists them.</summary>
    public ImmutableArray<McpResourceDefinition> Resources => _all.Resources;

    /// <summary>The prompts as the hotel lists them, with their arguments.</summary>
    public ImmutableArray<McpPromptDefinition> Prompts => _all.Prompts;

    /// <summary>The same tools with <paramref name="resources"/> as well.</summary>
    public McpToolTester WithResources(params IMcpResource[] resources) =>
        new(
            _all with
            {
                Resources =
                [
                    .. _all.Resources,
                    .. resources.Select(x => McpResourceDefinition.Create(x, "test")),
                ],
            }
        );

    /// <summary>The same tools with <paramref name="prompts"/> as well.</summary>
    public McpToolTester WithPrompts(params IMcpPrompt[] prompts) =>
        new(
            _all with
            {
                Prompts =
                [
                    .. _all.Prompts,
                    .. prompts.Select(x => McpPromptDefinition.Create(x, "test")),
                ],
            }
        );

    /// <summary>A staff member holding exactly <paramref name="nodes"/> (<c>*</c> holds them all).</summary>
    public McpTestCaller As(PlayerId player, params string[] nodes)
    {
        var held = new HashSet<string>(nodes, StringComparer.Ordinal);

        return new McpTestCaller(
            _all,
            new McpCaller(player),
            node => held.Contains(node) || held.Contains("*")
        );
    }

    /// <summary>A fake hotel's player, holding its <see cref="FakePlayer.Permissions"/> as they are at each call.</summary>
    public McpTestCaller As(FakePlayer player) =>
        new(
            _all,
            new McpCaller(player.Id),
            node => player.Permissions.Contains(node) || player.Permissions.Contains("*")
        );

    /// <summary>
    /// The tools, resources and prompts as the hotel's <see cref="IMcpTools"/>,
    /// <see cref="IMcpResources"/> and <see cref="IMcpPrompts"/>, each caller's nodes asked of
    /// <paramref name="hotel"/>: what an MCP server plugin's own tests serve.
    /// </summary>
    public McpTestRegistry Registry(FakeHotel hotel) => new(_all, hotel);

    internal static JsonElement? Arguments(object? arguments) =>
        arguments switch
        {
            null => null,
            JsonElement element => element,
            string json => JsonSerializer.Deserialize<JsonElement>(json),
            _ => JsonSerializer.SerializeToElement(arguments, arguments.GetType(), ARGUMENTS),
        };

    /// <summary>Values written as an object (<c>new { roomId = 1 }</c>) as the text MCP sends a prompt.</summary>
    internal static IReadOnlyDictionary<string, string>? Texts(object? arguments) =>
        arguments switch
        {
            null => null,
            IReadOnlyDictionary<string, string> texts => texts,
            _ => Arguments(arguments) is { ValueKind: JsonValueKind.Object } json
                ? json.EnumerateObject()
                    .ToDictionary(
                        x => x.Name,
                        x =>
                            x.Value.ValueKind == JsonValueKind.String
                                ? x.Value.GetString()!
                                : x.Value.GetRawText()
                    )
                : throw new ArgumentException(
                    "A prompt's arguments are an object of named values.",
                    nameof(arguments)
                ),
        };
}

/// <summary>What a tester holds: its tools, resources and prompts.</summary>
internal sealed record McpTestContributions(
    ImmutableArray<McpToolDefinition> Tools,
    ImmutableArray<McpResourceDefinition> Resources,
    ImmutableArray<McpPromptDefinition> Prompts
)
{
    public McpResourceDefinition? ResourceAt(string uri) =>
        Resources.FirstOrDefault(x => !x.IsTemplate && x.Matches(uri))
        ?? Resources.FirstOrDefault(x => x.IsTemplate && x.Matches(uri));
}

/// <summary>
/// A tester's tools, resources and prompts as the hotel's registries, each caller's nodes asked
/// of a fake hotel: what an MCP server plugin's tests serve.
/// </summary>
public sealed class McpTestRegistry : IMcpTools, IMcpResources, IMcpPrompts
{
    private readonly McpTestContributions _all;
    private readonly FakeHotel _hotel;

    internal McpTestRegistry(McpTestContributions all, FakeHotel hotel)
    {
        _all = all;
        _hotel = hotel;
    }

    public ImmutableArray<McpToolDefinition> All => _all.Tools;

    ImmutableArray<McpResourceDefinition> IMcpResources.All => _all.Resources;

    ImmutableArray<McpPromptDefinition> IMcpPrompts.All => _all.Prompts;

    public async Task<ImmutableArray<McpToolDefinition>> AvailableToAsync(
        McpCaller caller,
        CancellationToken ct
    )
    {
        var available = ImmutableArray.CreateBuilder<McpToolDefinition>();

        foreach (var tool in _all.Tools)
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

        foreach (var resource in _all.Resources)
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

        foreach (var prompt in _all.Prompts)
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
        var tool = _all.Tools.FirstOrDefault(x => x.Name == name);

        if (
            tool is null
            || !await tool.IsAvailableToAsync(caller, Holds(caller), ct).ConfigureAwait(false)
        )
            return McpToolCallResult.Unavailable(name);

        // No approvals in the test kit: a tool that needs one answers as the hotel does, pending.
        if (tool.RequiresApproval)
            return tool.CheckArguments(arguments, out _)
                ?? new McpToolCallResult(
                    McpToolCallStatus.PendingApproval,
                    [
                        $"{tool.Name} was not run yet: it needs a person's approval. Pending approval 1.",
                    ],
                    JsonSerializer.SerializeToElement(new { approvalId = 1, state = "pending" })
                );

        return await tool.CallAsync(arguments, new McpToolContext(caller, Holds(caller)), ct)
            .ConfigureAwait(false);
    }

    public async Task<McpResourceReadResult> ReadAsync(
        string uri,
        McpCaller caller,
        CancellationToken ct
    )
    {
        if (
            _all.ResourceAt(uri) is not { } resource
            || !await resource.IsAvailableToAsync(caller, Holds(caller), ct).ConfigureAwait(false)
        )
            return McpResourceReadResult.NotFound(uri);

        return await resource
            .ReadAsync(uri, new McpToolContext(caller, Holds(caller)), ct)
            .ConfigureAwait(false);
    }

    public async Task<McpPromptGetResult> GetAsync(
        string name,
        IReadOnlyDictionary<string, string>? arguments,
        McpCaller caller,
        CancellationToken ct
    )
    {
        var prompt = _all.Prompts.FirstOrDefault(x => x.Name == name);

        if (
            prompt is null
            || !await prompt.IsAvailableToAsync(caller, Holds(caller), ct).ConfigureAwait(false)
        )
            return McpPromptGetResult.Unavailable(name);

        return await prompt
            .GetAsync(arguments, new McpToolContext(caller, Holds(caller)), ct)
            .ConfigureAwait(false);
    }

    private Func<string, CancellationToken, Task<bool>> Holds(McpCaller caller) =>
        (node, ct) => _hotel.Players.HasPermissionAsync(caller.Player, node, ct);
}

/// <summary>An assistant acting for one staff member: what it is offered and what its calls answer.</summary>
public sealed class McpTestCaller
{
    private readonly McpTestContributions _all;
    private readonly McpCaller _caller;
    private readonly Func<string, bool> _holds;

    internal McpTestCaller(McpTestContributions all, McpCaller caller, Func<string, bool> holds)
    {
        _all = all;
        _caller = caller;
        _holds = holds;
    }

    /// <summary>The same staff member through read-only access: only read-only tools.</summary>
    public McpTestCaller ReadOnly() => new(_all, _caller with { ReadOnly = true }, _holds);

    /// <summary>The names of the tools it is offered (<c>tools/list</c>).</summary>
    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
    {
        var names = new List<string>();

        foreach (var tool in _all.Tools)
            if (await tool.IsAvailableToAsync(_caller, Holds, ct).ConfigureAwait(false))
                names.Add(tool.Name);

        return names;
    }

    /// <summary>
    /// The addresses of the resources (<c>resources/list</c>) and templates
    /// (<c>resources/templates/list</c>) it may read.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListResourcesAsync(CancellationToken ct = default)
    {
        var uris = new List<string>();

        foreach (var resource in _all.Resources)
            if (await resource.IsAvailableToAsync(_caller, Holds, ct).ConfigureAwait(false))
                uris.Add(resource.Uri);

        return uris;
    }

    /// <summary>The names of the prompts it is offered (<c>prompts/list</c>).</summary>
    public async Task<IReadOnlyList<string>> ListPromptsAsync(CancellationToken ct = default)
    {
        var names = new List<string>();

        foreach (var prompt in _all.Prompts)
            if (await prompt.IsAvailableToAsync(_caller, Holds, ct).ConfigureAwait(false))
                names.Add(prompt.Name);

        return names;
    }

    /// <summary>
    /// Calls a tool (<c>tools/call</c>) with <paramref name="arguments"/>: an object written as
    /// JSON in camelCase (<c>new { name = "al" }</c>), a JSON string, or a <see cref="JsonElement"/>.
    /// What the tool throws is thrown. A tool that needs approval is run as approved: the test
    /// sees what it does once a staff member says yes.
    /// </summary>
    public async Task<McpTestCall> CallAsync(
        string name,
        object? arguments = null,
        CancellationToken ct = default
    )
    {
        var tool = _all.Tools.FirstOrDefault(x => x.Name == name);

        if (
            tool is null
            || !await tool.IsAvailableToAsync(_caller, Holds, ct).ConfigureAwait(false)
        )
            return new McpTestCall(name, McpToolCallResult.Unavailable(name));

        var result = await tool.CallAsync(
                McpToolTester.Arguments(arguments),
                new McpToolContext(_caller, Holds),
                ct
            )
            .ConfigureAwait(false);

        return new McpTestCall(name, result);
    }

    /// <summary>Reads a resource (<c>resources/read</c>). What the resource throws is thrown.</summary>
    public async Task<McpTestRead> ReadAsync(string uri, CancellationToken ct = default)
    {
        if (
            _all.ResourceAt(uri) is not { } resource
            || !await resource.IsAvailableToAsync(_caller, Holds, ct).ConfigureAwait(false)
        )
            return new McpTestRead(uri, McpResourceReadResult.NotFound(uri));

        return new McpTestRead(
            uri,
            await resource
                .ReadAsync(uri, new McpToolContext(_caller, Holds), ct)
                .ConfigureAwait(false)
        );
    }

    /// <summary>
    /// Gets a prompt (<c>prompts/get</c>) with <paramref name="arguments"/>, an object of named
    /// values sent as text as MCP sends them (<c>new { roomId = 42 }</c>). What it throws is thrown.
    /// </summary>
    public async Task<McpTestPrompt> GetPromptAsync(
        string name,
        object? arguments = null,
        CancellationToken ct = default
    )
    {
        var prompt = _all.Prompts.FirstOrDefault(x => x.Name == name);

        if (
            prompt is null
            || !await prompt.IsAvailableToAsync(_caller, Holds, ct).ConfigureAwait(false)
        )
            return new McpTestPrompt(name, McpPromptGetResult.Unavailable(name));

        return new McpTestPrompt(
            name,
            await prompt
                .GetAsync(McpToolTester.Texts(arguments), new McpToolContext(_caller, Holds), ct)
                .ConfigureAwait(false)
        );
    }

    private Task<bool> Holds(string node, CancellationToken ct) => Task.FromResult(_holds(node));
}

/// <summary>A call's answer, with assertions that say what came back instead.</summary>
public sealed class McpTestCall(string tool, McpToolCallResult result)
{
    private static readonly JsonSerializerOptions DATA = new(JsonSerializerDefaults.Web);

    public McpToolCallResult Result => result;

    public McpToolCallStatus Status => result.Status;

    /// <summary>Every text block, one per line.</summary>
    public string Text => string.Join("\n", result.Text);

    /// <summary>The resources it links to.</summary>
    public ImmutableArray<McpResourceLink> Links => result.Links;

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

/// <summary>A resource read's answer, with assertions that say what came back instead.</summary>
public sealed class McpTestRead(string uri, McpResourceReadResult result)
{
    private static readonly JsonSerializerOptions DATA = new(JsonSerializerDefaults.Web);

    public McpResourceReadResult Result => result;

    public McpResourceReadStatus Status => result.Status;

    /// <summary>The contents' text, or why there were none.</summary>
    public string Text =>
        result.Contents.IsEmpty
            ? result.Message ?? string.Empty
            : string.Join("\n", result.Contents.Select(x => x.Text));

    /// <summary>The contents, written as JSON, read as <typeparamref name="T"/>.</summary>
    public T Data<T>() =>
        JsonSerializer.Deserialize<T>(Text, DATA)
        ?? throw new HotelAssertionException($"{uri} answered null data.");

    public McpTestRead AssertOk() =>
        result.Status == McpResourceReadStatus.Ok
            ? this
            : throw new HotelAssertionException($"{uri} answered {result.Status}: {Text}");

    /// <summary>Nothing the caller may read is there, the message holding <paramref name="contains"/>.</summary>
    public McpTestRead AssertNotFound(string contains = "") =>
        result.Status == McpResourceReadStatus.NotFound
        && Text.Contains(contains, StringComparison.Ordinal)
            ? this
            : throw new HotelAssertionException(
                $"{uri} was expected to answer NotFound with \"{contains}\"; it answered {result.Status}: {Text}"
            );
}

/// <summary>A prompt's answer, with assertions that say what came back instead.</summary>
public sealed class McpTestPrompt(string name, McpPromptGetResult result)
{
    public McpPromptGetResult Result => result;

    public McpPromptGetStatus Status => result.Status;

    public ImmutableArray<McpPromptMessage> Messages => result.Messages;

    /// <summary>Every message's text (a link as its address), one per line; or why it was not made.</summary>
    public string Text =>
        result.Messages.IsEmpty
            ? result.Message ?? string.Empty
            : string.Join("\n", result.Messages.Select(x => x.Text ?? x.Link?.Uri));

    public McpTestPrompt AssertOk() =>
        result.Status == McpPromptGetStatus.Ok
            ? this
            : throw new HotelAssertionException($"{name} answered {result.Status}: {Text}");

    /// <summary>Its arguments were refused, the message holding <paramref name="contains"/>.</summary>
    public McpTestPrompt AssertInvalid(string contains) =>
        result.Status == McpPromptGetStatus.InvalidArguments
        && Text.Contains(contains, StringComparison.Ordinal)
            ? this
            : throw new HotelAssertionException(
                $"{name} was expected to answer InvalidArguments with \"{contains}\"; it answered {result.Status}: {Text}"
            );
}
