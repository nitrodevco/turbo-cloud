using System.IO;
using System.Text.Json;
using FluentAssertions;
using Turbo.Contracts.Approvals;
using Turbo.Contracts.Plugins.Mcp;
using Turbo.Primitives.Plugins;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// Approvals from compiled plugins (<c>docs/approvals.md</c>): an MCP tool that needs approval
/// answers "pending approval" over MCP and runs, with the arguments it was checked with, only when
/// another staff member approves; a plugin's approval handler is found as it loads, what the plugin
/// asks is recorded as its own, and once it unloads nothing carries its kind out.
/// </summary>
public sealed class ApprovalPluginTests : IDisposable
{
    private const string SHOP = """
        using System.ComponentModel;
        using System.ComponentModel.DataAnnotations;
        using System.Threading;
        using System.Threading.Tasks;
        using Turbo.Contracts.Approvals;
        using Turbo.Contracts.Hotel;
        using Turbo.Contracts.Plugins.Mcp;
        using Turbo.Primitives.Players;
        using Turbo.Primitives.Players.Permissions;

        namespace Shop;

        public sealed record RefundInput([Description("The player's id.")] int Player, [Range(1, 100000)] int Credits);

        [RequiresPermission("plugin.shop.refund")]
        public sealed class RefundTool(IHotel hotel) : McpTool<RefundInput>
        {
            public override string Name => "refund";
            public override string Description => "Gives a player credits back.";
            public override bool RequiresApproval => true;
            public override string ApprovalNode => "plugin.shop.approve";

            public override async Task<McpToolResult> HandleAsync(RefundInput input, McpToolContext ctx, CancellationToken ct)
            {
                await hotel.Wallet.CreditAsync(new PlayerId(input.Player), "credits", input.Credits, "refund", "refund", ct);

                return McpToolResult.Text($"Refunded {input.Credits} credits.");
            }
        }

        public sealed record GiftArgs(int Player, int Credits);

        [RequiresPermission("plugin.shop.refund")]
        public sealed class AskGiftTool(IApprovals approvals) : McpTool<GiftArgs>
        {
            public override string Name => "ask_gift";
            public override string Description => "Asks staff to gift a player credits.";

            public override async Task<McpToolResult> HandleAsync(GiftArgs input, McpToolContext ctx, CancellationToken ct)
            {
                var asked = await approvals.RequestAsync(new ApprovalRequest
                {
                    Kind = "shop.gift",
                    Summary = $"Gift player {input.Player} {input.Credits} credits",
                    Arguments = input,
                    Requester = ApprovalRequester.Staff(ctx.Caller),
                }, ct);

                return McpToolResult.Text(asked.Id.ToString());
            }
        }

        public sealed class GiftHandler(IHotel hotel) : ApprovalHandler<GiftArgs>
        {
            public override string Kind => "shop.gift";

            public override async Task<ApprovalResult> ExecuteAsync(GiftArgs args, Approval approval, CancellationToken ct)
            {
                await hotel.Wallet.CreditAsync(new PlayerId(args.Player), "credits", args.Credits, $"gift:{approval.Id}", "gift", ct);

                return ApprovalResult.Done($"Gifted {args.Credits}.");
            }
        }
        """;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-approvals-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task An_mcp_tool_needing_approval_answers_pending_and_runs_once_another_staff_member_approves()
    {
        var host = new PluginTestHost(_db, _fakes);
        var staff = host.Hotel.Players.Add("Staff");
        var approver = host.Hotel.Players.Add("Approver");
        var alice = host.Hotel.Players.Add("Alice");

        staff.Permissions.Add("plugin.shop.refund");
        staff.Permissions.Add("plugin.shop.approve");
        approver.Permissions.Add("plugin.shop.approve");
        await LoadAsync(host);

        var call = await host.Mcp.CallAsync(
            "refund",
            JsonSerializer.SerializeToElement(new { player = alice.Id.Value, credits = 500 }),
            new McpCaller(staff.Id),
            Ct
        );

        call.Status.Should().Be(McpToolCallStatus.PendingApproval);
        call.IsError.Should().BeFalse();

        var id = call.Structured!.Value.GetProperty("approvalId").GetInt32();

        call.Text.Single()
            .Should()
            .StartWith(
                $"refund was not run yet: it needs a person's approval. Pending approval {id};"
            );
        host.Hotel.Wallet.Balance(alice.Id, "credits").Should().Be(0);

        var asked = (await host.Approvals.GetAsync(id, Ct))!;

        (asked.Kind, asked.Plugin, asked.Node, asked.Requester)
            .Should()
            .Be(
                (
                    McpToolApproval.KIND,
                    "shop",
                    "plugin.shop.approve",
                    ApprovalRequester.Token(staff.Id, "MCP")
                )
            );
        (await host.Approvals.ApproveAsync(id, staff.Id, null, Ct))
            .Status.Should()
            .Be(ApprovalDecisionStatus.OwnRequest);

        var approved = await host.Approvals.ApproveAsync(id, approver.Id, null, Ct);

        (approved.Status, approved.Approval!.Result)
            .Should()
            .Be((ApprovalDecisionStatus.Executed, "Refunded 500 credits."));
        host.Hotel.Wallet.Balance(alice.Id, "credits").Should().Be(500);
        (
            await host.Mcp.CallAsync(
                "refund",
                JsonSerializer.SerializeToElement(new { player = alice.Id.Value }),
                new McpCaller(staff.Id),
                Ct
            )
        )
            .Status.Should()
            .Be(McpToolCallStatus.InvalidArguments);
        (await host.Approvals.CountPendingAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task A_plugins_handler_carries_out_what_it_asked_for_and_nothing_does_once_it_unloads()
    {
        var host = new PluginTestHost(_db, _fakes);
        var staff = host.Hotel.Players.Add("Staff");
        var approver = host.Hotel.Players.Add("Approver");
        var alice = host.Hotel.Players.Add("Alice");

        staff.Permissions.Add("plugin.shop.refund");
        approver.Permissions.Add(
            Turbo.Primitives.Players.Permissions.PermissionNodes.Approvals.DECIDE
        );

        var manager = await LoadAsync(host);

        manager
            .ContributionsOf("shop")!
            .Items.Should()
            .ContainEquivalentOf(
                new PluginContribution(
                    PluginContributionKinds.APPROVAL_HANDLER,
                    "shop.gift",
                    "GiftHandler"
                )
            );

        var first = await AskGiftAsync(host, staff, alice, 25);
        var second = await AskGiftAsync(host, staff, alice, 30);

        (await host.Approvals.GetAsync(first, Ct))!.Plugin.Should().Be("shop");
        (await host.Approvals.ApproveAsync(first, approver.Id, null, Ct))
            .Status.Should()
            .Be(ApprovalDecisionStatus.Executed);
        host.Hotel.Wallet.Balance(alice.Id, "credits").Should().Be(25);

        await manager.UnloadAllAsync(Ct);

        (await host.Approvals.ApproveAsync(second, approver.Id, null, Ct))
            .Status.Should()
            .Be(ApprovalDecisionStatus.NoHandler);
        (await host.Approvals.GetAsync(second, Ct))!.State.Should().Be(ApprovalState.Pending);
        host.Hotel.Wallet.Balance(alice.Id, "credits").Should().Be(25);
    }

    private async Task<int> AskGiftAsync(
        PluginTestHost host,
        Turbo.Testing.FakePlayer staff,
        Turbo.Testing.FakePlayer player,
        int credits
    )
    {
        var call = await host.Mcp.CallAsync(
            "ask_gift",
            JsonSerializer.SerializeToElement(new { player = player.Id.Value, credits }),
            new McpCaller(staff.Id),
            Ct
        );

        call.Status.Should().Be(McpToolCallStatus.Ok);

        return int.Parse(call.Text.Single(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<Turbo.Plugins.PluginManager> LoadAsync(PluginTestHost host)
    {
        PluginBuilder.Write(_folder, "shop", SHOP);

        var manager = host.Manager(_folder);

        await manager.LoadAllAsync(ct: Ct);

        return manager;
    }
}
