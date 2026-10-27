---
name: turbo-ai-agents
description: Ask language models from a Turbo plugin with IAi - an AiAgent class (instructions, McpTool tools, MaxSteps, budgets, middleware, stop conditions), AiCall (who it acts as, memory, overrides), typed answers with AiAgent<TOutput>, player text as PlayerText, approvals for tools that change things - and test it with AiFake. Use when a plugin needs an NPC that talks, a staff assistant, a generated text or a model's typed judgement; never to call a provider's API yourself.
---

# AI agents in a plugin

## When to use this skill

The plugin should answer players in words (a concierge NPC), help staff ("find the room, kick the
spammer"), write a text (a room's description) or judge something into fields (a review score).
The hotel brings the providers and the keys (`Turbo:Ai`); a plugin never names a provider, never
holds a key and never calls an AI API with `HttpClient`. Declare the use in `manifest.json`, which
the hotel's owner reads before allowing the plugin (`Turbo:Ai:Plugins`):

```json
"ai": { "purpose": "Answers players' questions about the hotel's public rooms." }
```

## An agent and its tool

<!-- compiled as the plugin "demo" -->

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Ai;
using Turbo.Contracts.Hotel;
using Turbo.Contracts.Plugins.Mcp;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Demo;

public sealed record FindRoomInput([Description("Words of the room's name.")] [StringLength(40, MinimumLength = 2)] string Name);

public sealed record FindRoomOutput(IReadOnlyList<FoundRoom> Rooms);

public sealed record FoundRoom(int Id, PlayerText Name);

[RequiresPermission(DemoAiNodes.FIND_ROOM)]
public sealed class FindRoomTool(IHotel hotel) : McpTool<FindRoomInput, FindRoomOutput>
{
    public override string Name => "demo_find_room";

    public override string Description => "Finds the hotel's public rooms whose name holds the given words: id and name.";

    public override bool ReadOnly => true;

    public override async Task<McpToolResult> HandleAsync(FindRoomInput input, McpToolContext ctx, CancellationToken ct)
    {
        var found = new List<FoundRoom>();

        foreach (var id in await hotel.Rooms.PublicIdsAsync(ct))
            if (await hotel.Rooms.GetAsync(id, ct) is { } room && room.Name.Contains(input.Name, System.StringComparison.OrdinalIgnoreCase))
                found.Add(new FoundRoom(room.Id.Value, room.Name));

        return found.Count == 0
            ? McpToolResult.Error($"No public room's name holds \"{input.Name}\". Try one word.")
            : Ok(new FindRoomOutput(found));
    }
}

public sealed class ConciergeAgent(IHotel hotel) : AiAgent
{
    public override string Instructions =>
        "You are the hotel's concierge. Answer in two sentences at most. Look rooms up with demo_find_room; never invent one.";

    public override IEnumerable<IMcpTool> Tools => [new FindRoomTool(hotel)];

    public override int MaxSteps => 4;

    public override AiBudget? Budget => new(Tokens: 4000);
}

public sealed class Concierge(IAi ai, IHotel hotel)
{
    public async Task<string> AnswerAsync(PlayerId player, string question, CancellationToken ct)
    {
        var response = await ai.PromptAsync(
            new ConciergeAgent(hotel),
            new AiPrompt("A player asks the concierge:", question),
            new AiCall
            {
                As = AiActor.System(DemoAiNodes.FIND_ROOM),
                For = player,
                Memory = AiMemory.ForPlayer(player),
            },
            ct
        );

        return response.Stop switch
        {
            AiStopReason.Finished => response.Text,
            AiStopReason.DailyBudget or AiStopReason.RateLimited => response.Message!,
            _ => "The concierge can't answer right now.",
        };
    }
}
```

The node, declared like any other:

```csharp
using System.Collections.Generic;
using Turbo.Primitives.Players.Permissions;

namespace Demo;

public sealed class DemoAiNodes : IPermissionNodeSource
{
    public const string FIND_ROOM = "demo.ai.find_room";

    public string? Prefix => "demo";

    public IEnumerable<PermissionNodeDefinition> Nodes => [new(FIND_ROOM, "Find public rooms by name (staff assistants and the concierge)")];

    public IEnumerable<PermissionMetaDefinition> MetaKeys => [];
}
```

## A typed answer

```csharp
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Turbo.Contracts.Ai;

namespace Demo;

public sealed record RoomReview([Description("1 to 10.")] [Range(1, 10)] int Score, [Description("One sentence for the owner.")] string Feedback);

public sealed class RoomReviewer : AiAgent<RoomReview>
{
    public override string Instructions => "You review rooms for the hotel's room of the week.";
}
```

`var review = await ai.PromptAsync(new RoomReviewer(), new AiPrompt("Review this room:", description), ct: ct);`
then `review.Output` (null unless `review.Ok`). One-shot without a class: `ai.AskAsync(prompt)` and
`ai.AskAsync<TOutput>(prompt)`.

## The rules

- **Who it acts as** (`AiCall.As`): `AiActor.AsPlayer(id)` (the player's nodes, asked at every tool
  call), `AiActor.System("node", ...)` (exactly those nodes), or nobody (no tools, the default). A
  tool is offered only when that identity holds its node. `AiCall.For` is the player whose daily
  tokens and rate limit the call counts against.
- **Tools are MCP tools**: `McpTool<TIn, TOut>` with `[RequiresPermission]` (skill
  turbo-mcp-tools). The same class serves staff's assistants over MCP. Bad arguments go back to the
  model as sentences; the tool never sees them.
- **Tools that change things wait on approval**: a tool not `ReadOnly` and not
  `Destructive => false` stops the run with `AwaitingApproval`; once staff decide, the run goes on
  and the agent's `ResumedAsync(response, ct)` gets the answer (override it to tell the player).
  Mark a tool that only adds (a whisper, a ticket) `Destructive => false`.
- **Player text is `PlayerText`**: `new AiPrompt("your words", playerText)` or
  `AiPrompt.FromPlayer(text)`, never the player's words pasted into a string. Tools answer player
  text as `PlayerText` too.
- **Never throws for a model**: check `response.Stop` (`Finished`, `Truncated`, `MaxSteps`,
  `Budget`, `DailyBudget`, `RateLimited`, `StopCondition`, `AwaitingApproval`, `Cancelled`,
  `Unavailable`, `Failed`). `Unavailable`: the plugin isn't allowed, declares no `"ai"`, or the hotel
  has no provider.
- **Memory**: `AiMemory.ForPlayer(id)` / `ForKey("room:12")` keeps the conversation; only its
  owner goes on with it (`Continue(id)`); `ai.ForgetAsync(agent, memory)` deletes it. Add
  `new AiSummarize(keep: 20)` to `Middleware` to summarise long ones.
- **Limits**: `MaxSteps`, `MaxTokens`, `Budget` (tokens, cost), `StopWhen` (`AiStopWhen.ToolCalled`,
  `TokensOver`, `When`); the hotel caps steps and sets each player's daily tokens.
- Calls wait on the network: call `IAi` from a handler, a command, a task or a web route, never
  inside a room's turn.

## Test with AiFake

```csharp test
using System.Threading.Tasks;
using Turbo.Contracts.Ai;
using Turbo.Testing;
using Xunit;

namespace Demo.Tests;

public sealed class ConciergeTests
{
    [Fact]
    public async Task The_concierge_looks_the_room_up_and_answers_with_it()
    {
        var hotel = new FakeHotel();
        hotel.Rooms.Add("Snowstorm Lobby", hotel.Players.Add("Owner"), isPublic: true);
        var player = hotel.Players.Add("Alice");
        var ai = new AiFake(hotel).PreventStrayPrompts();
        ai.CallTool("demo_find_room", new { name = "Snowstorm" }).Respond("Try the Snowstorm Lobby.");

        var answer = await new Concierge(ai.For("demo"), hotel).AnswerAsync(player.Id, "where is snowstorm?", default);

        Assert.Equal("Try the Snowstorm Lobby.", answer);
        ai.AssertPrompted("where is snowstorm?");
        Assert.Equal(AiToolOutcome.Ok, ai.AssertToolCalled("demo_find_room").Outcome);
    }

    [Fact]
    public async Task A_review_out_of_range_is_sent_back_and_the_fixed_one_kept()
    {
        var ai = new AiFake();
        ai.Answer(new { score = 12, feedback = "Wow" }).Answer(new { score = 9, feedback = "Wow" });

        var review = await ai.For("demo").PromptAsync(new RoomReviewer(), "Review: a pool with a slide");

        Assert.Equal(new RoomReview(9, "Wow"), review.Output);
    }
}
```

Script: `Respond(text)`, `CallTool(name, args)`, `Answer(output)`, `Truncate(text)`, `Fail(kind)`,
`Then(request => answer)`, `PreventStrayPrompts()`. Assert: `AssertPrompted`, `AssertNotPrompted`,
`AssertNeverPrompted`, `AssertToolCalled(name, args?)`, `AssertToolNotRun`, `AssertStoppedBy`; read
`Requests`, `Responses`, `Usage`. Approvals: `ai.Approvals.ApproveAsync()` / `DenyAsync(note: ...)`
resume the run. Hotel side: `ai.Settings = new() { PlayerDailyTokens = 100 }`, `ai.Refuse("demo")`.
A `TestHotel` has one: `hotel.Ai`. Guide: `ai-sdk.md`.
