using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Turbo.Contracts.Ai;
using Turbo.Testing;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// A plugin's language models as the server gives them (<c>docs/ai-sdk.md</c>): a plugin declaring
/// <c>"ai"</c> gets its own <see cref="IAi"/> by constructor, its agent's tool that changes things
/// waits on a staff member's approval, and once approved the run goes on by itself with the agent
/// built from the plugin's services, which tells whoever asked.
/// </summary>
public sealed class PluginAiTests : IAsyncLifetime
{
    private const string MANIFEST = """
        {
          "Name": "Desk",
          "Key": "desk",
          "Version": "1.0.0",
          "Author": "Staff",
          "ApiVersion": 1,
          "Ai": { "Purpose": "A staff assistant that alerts players when asked." }
        }
        """;

    private const string SOURCE = """
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Turbo.Contracts.Ai;
        using Turbo.Contracts.Hotel;
        using Turbo.Contracts.Plugins.Mcp;
        using Turbo.Primitives.Commands;
        using Turbo.Primitives.Players;
        using Turbo.Primitives.Players.Permissions;

        namespace Desk;

        public sealed record AlertInput(int Player, string Text);

        [RequiresPermission("plugin.desk.alert")]
        public sealed class AlertTool(IHotel hotel) : McpTool<AlertInput>
        {
            public override string Name => "desk_alert";

            public override string Description => "Alerts a player with a text.";

            public override async Task<McpToolResult> HandleAsync(AlertInput input, McpToolContext ctx, CancellationToken ct)
            {
                await hotel.Notify.AlertAsync(new PlayerId(input.Player), "desk.alert", input.Text, [], ct);

                return McpToolResult.Text("Alerted.");
            }
        }

        public sealed class DeskAgent(IHotel hotel) : AiAgent
        {
            public override string Instructions => "You help staff alert players.";

            public override IEnumerable<IMcpTool> Tools => [new AlertTool(hotel)];

            public override async Task ResumedAsync(AiResumed resumed, CancellationToken ct)
            {
                if (resumed.Player is { } player)
                    await hotel.Notify.AlertAsync(player, "desk.done", resumed.Response.Text, [], ct);
            }
        }

        [Command("desk", Description = "Ask the desk")]
        [RequiresPermission("plugin.desk.use")]
        public sealed class DeskCommand(IAi ai, IHotel hotel) : ICommand<DeskCommand.Arguments>
        {
            public sealed record Arguments(string Ask);

            public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
                new Dictionary<string, string> { ["answer"] = "%0% %1%" };

            public async ValueTask<CommandResult> ExecuteAsync(ICommandContext ctx, Arguments arguments, CancellationToken ct)
            {
                var response = await ai.PromptAsync(
                    new DeskAgent(hotel),
                    new AiPrompt("Staff asks:", arguments.Ask),
                    new AiCall { As = AiActor.AsPlayer(ctx.Executor.PlayerId) },
                    ct
                );

                return CommandResult.Done("answer", response.Stop.ToString(), response.Pending?.Tool ?? response.Text);
            }
        }
        """;

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-ai-" + Guid.NewGuid().ToString("N")
    );

    private readonly TestHotel _hotel = TestHotel.Create();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "server"));
        File.WriteAllText(Path.Combine(_folder, "manifest.json"), MANIFEST);
        File.WriteAllText(Path.Combine(_folder, "server", "Desk.cs"), SOURCE);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _hotel.DisposeAsync();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task An_agents_tool_waits_on_approval_and_once_approved_runs_and_tells_the_staff_member()
    {
        await _hotel.LoadAsync(_folder, ct: Ct);
        var alice = _hotel.Player("alice");
        var staff = _hotel
            .Player("staff")
            .InRoom(5)
            .WithPermissions("plugin.desk.use", "plugin.desk.alert");
        _hotel
            .Ai.CallTool("desk_alert", new { player = alice.Id.Value, text = "Welcome!" })
            .Respond("Alice was alerted.");

        await staff.SaysAsync(":desk welcome-alice", Ct);

        staff.Should().HaveBeenWhispered("AwaitingApproval desk_alert");
        _hotel.Notify.Alerts.Should().BeEmpty("nothing runs before a staff member approves");
        _hotel.Ai.Approvals.Requests.Single().Request.Nodes.Should().Equal("plugin.desk.alert");

        await _hotel.Ai.Approvals.ApproveAsync();

        _hotel
            .Notify.Alerts.Select(a => (a.Player, a.Text))
            .Should()
            .Equal((alice.Id, "Welcome!"), (staff.Id, "Alice was alerted."));
    }

    [Fact]
    public async Task A_plugin_the_hotel_has_not_allowed_sends_nothing()
    {
        _hotel.Ai.Refuse("desk");
        await _hotel.LoadAsync(_folder, ct: Ct);
        var staff = _hotel.Player("staff").InRoom(5).WithPermissions("plugin.desk.use");

        await staff.SaysAsync(":desk hello", Ct);

        staff.Should().HaveBeenWhispered("Unavailable ");
        _hotel.Ai.AssertNeverPrompted();
    }
}
