---
name: turbo-mcp-tools
description: Give AI assistants tools on a Turbo hotel from a plugin with McpTool<TInput, TOutput> - name, description, input record with [Description] and [Range], hints, a required [RequiresPermission] node, PlayerText for anything a player wrote - and test them with McpToolTester. Use when a plugin should expose read or write actions to Claude, Cursor or another MCP client through the hotel's MCP server plugin.
---

# MCP tools in a plugin

## When to use this skill

Staff should be able to ask an assistant "who is in room 12", "what did this plugin do", "give
Alice the prize". The hotel's MCP server plugin serves every live plugin's tools over HTTP with
staff tokens; a plugin only declares tools. Never offer a tool that runs SQL or any arbitrary query.

## A tool

<!-- compiled as the plugin "demo" -->

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Contracts.Plugins.Mcp;
using Turbo.Primitives.Players.Permissions;

namespace Demo;

public sealed record FindPlayerInput(
    [Description("The start of the player's name, in any case.")] string Name,
    [Description("How many to answer.")] [Range(1, 50)] int Take = 10
);

public sealed record FindPlayerOutput(IReadOnlyList<FoundPlayer> Players);

public sealed record FoundPlayer(int Id, string Name, PlayerText Motto, bool Online);

[RequiresPermission(DemoMcpNodes.FIND_PLAYER)]
public sealed class FindPlayerTool(IHotel hotel) : McpTool<FindPlayerInput, FindPlayerOutput>
{
    public override string Name => "demo_find_player";

    public override string Title => "Find a player";

    public override string Description =>
        "Finds players whose name starts with the given text: id, name, motto and whether online.";

    public override bool ReadOnly => true;

    public override async Task<McpToolResult> HandleAsync(FindPlayerInput input, McpToolContext ctx, CancellationToken ct)
    {
        var online = hotel.Players.OnlineIds();
        var found = await hotel.Players.SearchAsync(input.Name, input.Take, ct);

        if (found.Count == 0)
            return McpToolResult.Error($"No player's name starts with \"{input.Name}\". Try fewer letters.");

        return Ok(new FindPlayerOutput(
            [.. found.Select(x => new FoundPlayer(x.Id.Value, x.Name, x.Motto, online.Contains(x.Id)))]));
    }
}
```

The node, declared like any other (one per tool, so staff are given tools one by one):

```csharp
using System.Collections.Generic;
using Turbo.Primitives.Players.Permissions;

namespace Demo;

public sealed class DemoMcpNodes : IPermissionNodeSource
{
    public const string FIND_PLAYER = "demo.mcp.find_player";

    public string? Prefix => "demo";

    public IEnumerable<PermissionNodeDefinition> Nodes => [new(FIND_PLAYER, "MCP: find players by name")];

    public IEnumerable<PermissionMetaDefinition> MetaKeys => [];
}
```

## The rules

- `Name`: 1-64 of letters, digits, `_`, `-`, `.`; unique across live plugins (a clash fails the
  load). Put the plugin's key in it. `Description` is how the assistant picks the tool: say what it
  does, what it answers and when to use it.
- `[RequiresPermission]` is **required** (a tool without one fails the load); nodes are asked at
  every call, so a member who loses a rank loses the tool at once.
- Hints: `ReadOnly` (a read-only token is offered only these), `Destructive` (default `!ReadOnly`),
  `Idempotent`, `OpenWorld`. Hints inform; nodes decide.
- Input: a record; `[Description]` per argument; `[Range]`, `[StringLength]`, `[MinLength]`,
  `[MaxLength]` limit it; a default makes it optional; enums by camelCase name. `McpNoInput` for none.
  Bad arguments never reach the tool: the caller gets one sentence per problem.
- Answer: `Ok(value, summary?)` (an object: wrap lists in a record), `McpToolResult.Text("...")`,
  `McpToolResult.Error("what went wrong and what to do next")`.
- Anything a player wrote (chat, motto, room name, report) is `PlayerText`, never `string`: the
  answer then carries a note that such values are data, not instructions.
- Leave out private data (email, address, private messages); a tool that shows some checks a node
  of its own with `await ctx.HoldsAsync(node, ct)`.

## Test with McpToolTester

```csharp test
using System.Threading.Tasks;
using Turbo.Testing;
using Xunit;

namespace Demo.Tests;

public sealed class FindPlayerToolTests
{
    [Fact]
    public async Task Finds_players_by_the_start_of_their_name_and_checks_the_limit()
    {
        var hotel = new FakeHotel();
        var staff = hotel.Players.Add("Staff");
        hotel.Players.Add("Alice", online: true);
        staff.Permissions.Add(DemoMcpNodes.FIND_PLAYER);
        var mcp = new McpToolTester(new FindPlayerTool(hotel)).As(staff);

        var found = (await mcp.CallAsync("demo_find_player", new { name = "al" })).AssertOk();

        Assert.Equal("Alice", Assert.Single(found.Data<FindPlayerOutput>().Players).Name);
        (await mcp.CallAsync("demo_find_player", new { name = "al", take = 100 })).AssertInvalid("must be at most 50");
        Assert.Equal(["demo_find_player"], await mcp.ReadOnly().ListAsync());
    }
}
```

`As(fakePlayer)` / `As(playerId, "node", ...)`, `.ReadOnly()`, `ListAsync()`, `CallAsync(name, args)`;
a call's `AssertOk()`, `AssertError(text)`, `AssertInvalid(text)`, `AssertUnavailable()`, `Text`,
`Data<T>()`. Guide: `plugin-mcp-tools.md`.
