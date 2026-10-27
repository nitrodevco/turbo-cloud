using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Ai;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Mcp;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Testing;
using Xunit;

namespace Turbo.Tests.Ai;

/// <summary>
/// Conversations an agent keeps (<c>docs/ai-sdk.md</c>, Memory): one per participant, going on
/// where it was, only for its owner, bounded by <c>Turbo:Ai:MemoryMessages</c>, forgotten on
/// request; and a run that waited on an approval goes on by itself with its plugin's agent.
/// </summary>
public sealed class AiMemoryTests
{
    private readonly FakeHotel _hotel = new();
    private readonly AiFake _ai;

    public AiMemoryTests() => _ai = new AiFake(_hotel);

    [Fact]
    public async Task A_players_conversation_goes_on_where_it_was_and_another_players_is_their_own()
    {
        var alice = _hotel.Players.Add("Alice");
        var bob = _hotel.Players.Add("Bob");
        _ai.Respond("Hello Alice").Respond("You said hi").Respond("Hello Bob");
        var ai = _ai.For();

        var first = await ai.PromptAsync(new Chat(), "hi", new AiCall { Memory = AiMemory.ForPlayer(alice.Id) });
        await ai.PromptAsync(new Chat(), "what did I say?", new AiCall { Memory = AiMemory.ForPlayer(alice.Id) });
        await ai.PromptAsync(new Chat(), "hi", new AiCall { Memory = AiMemory.ForPlayer(bob.Id) });

        _ai.Requests[1].Messages.Select(m => m.Text).Should().Equal("hi", "Hello Alice", "what did I say?");
        _ai.Requests[2].Messages.Select(m => m.Text).Should().Equal("hi");
        _ai.Usage.Select(u => u.ConversationId).Should().Equal(first.ConversationId, first.ConversationId, first.ConversationId + 1);
    }

    [Fact]
    public async Task A_conversation_is_resumed_only_by_its_owner()
    {
        var alice = _hotel.Players.Add("Alice");
        var bob = _hotel.Players.Add("Bob");
        var mine = await _ai.For().PromptAsync(new Chat(), "secret", new AiCall { Memory = AiMemory.ForPlayer(alice.Id) });

        var stolen = await _ai.For()
            .PromptAsync(new Chat(), "what was it?", new AiCall { Memory = AiMemory.ForPlayer(bob.Id).Continue(mine.ConversationId!.Value) });
        var otherPlugin = await _ai.For("other-plugin")
            .PromptAsync(new Chat(), "what was it?", new AiCall { Memory = AiMemory.ForPlayer(alice.Id).Continue(mine.ConversationId!.Value) });

        stolen.Stop.Should().Be(AiStopReason.Unavailable);
        otherPlugin.Stop.Should().Be(AiStopReason.Unavailable);
        _ai.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_conversation_keeps_the_most_messages_from_the_start_of_a_turn()
    {
        var alice = _hotel.Players.Add("Alice");
        _ai.Settings = new() { MemoryMessages = 3, PlayerRunsPerMinute = 0, PluginRunsPerMinute = 0 };
        var call = new AiCall { Memory = AiMemory.ForPlayer(alice.Id) };

        foreach (var said in new[] { "one", "two", "three" })
            await _ai.For().PromptAsync(new Chat(), said, call);

        // Before "three": one, a1, two, a2 is 4 > 3: the oldest whole turn goes.
        _ai.Requests[2].Messages.Select(m => m.Text).Should().Equal("two", "Fake answer.", "three");
    }

    [Fact]
    public async Task A_forgotten_conversation_starts_again()
    {
        var alice = _hotel.Players.Add("Alice");
        var memory = AiMemory.ForPlayer(alice.Id);
        await _ai.For().PromptAsync(new Chat(), "remember 42", new AiCall { Memory = memory });

        (await _ai.For().ForgetAsync(new Chat(), memory)).Should().BeTrue();
        await _ai.For().PromptAsync(new Chat(), "what number?", new AiCall { Memory = memory });

        _ai.Requests[1].Messages.Select(m => m.Text).Should().Equal("what number?");
        _ai.Store.Conversations.Should().ContainSingle();
    }

    [Fact]
    public async Task An_approved_run_goes_on_by_itself_with_its_plugins_agent_which_is_told_the_answer()
    {
        var staff = _hotel.Players.Add("Staff");
        staff.Permissions.Add("test.kick");
        var told = new List<AiResponse>();
        var services = new ServiceCollection().AddSingleton(told).BuildServiceProvider();
        var manifest = new PluginManifest
        {
            Name = "Desk",
            Key = "desk",
            Version = "1.0.0",
            Author = "test",
            Ai = new PluginAiUse { Purpose = "Helps staff." },
        };
        using var live = _ai.Live(manifest, typeof(KickAgent).Assembly, services);
        _ai.CallTool("kick", new { name = "Bob" }).Respond("Bob was kicked.");

        var paused = await _ai.ForPlugin(manifest)
            .PromptAsync(new KickAgent(told), "Kick Bob", new AiCall { As = AiActor.AsPlayer(staff.Id) });
        var resumed = await _ai.Approvals.ApproveAsync(by: new PlayerId(1));

        paused.Stop.Should().Be(AiStopReason.AwaitingApproval);
        paused.ConversationId.Should().NotBeNull("a run without memory keeps one while it waits");
        resumed!.Stop.Should().Be(AiStopReason.Finished);
        told.Should().ContainSingle().Which.Text.Should().Be("Bob was kicked.");
        _ai.AssertToolCalled("kick", a => a.GetProperty("name").GetString() == "Bob").Outcome.Should().Be(AiToolOutcome.Ok);
    }

    private sealed class Chat : AiAgent
    {
        public override string Instructions => "Chat.";
    }

    public sealed class KickAgent(List<AiResponse> told) : AiAgent
    {
        public override string Instructions => "You kick players when staff ask.";

        public override IEnumerable<IMcpTool> Tools => [new KickTool()];

        public override Task ResumedAsync(AiResponse response, CancellationToken ct)
        {
            told.Add(response);

            return Task.CompletedTask;
        }
    }

    public sealed record KickInput(string Name);

    [RequiresPermission("test.kick")]
    public sealed class KickTool : McpTool<KickInput>
    {
        public override string Name => "kick";

        public override string Description => "Kicks a player from their room.";

        public override Task<McpToolResult> HandleAsync(KickInput input, McpToolContext ctx, CancellationToken ct) =>
            Task.FromResult(McpToolResult.Text($"{input.Name} was kicked."));
    }
}
