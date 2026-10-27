using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Approvals;
using Turbo.Contracts.Plugins.Mcp;

namespace Turbo.Plugins.Approvals;

/// <summary>
/// Runs an MCP tool's call once a person approves it (<see cref="IMcpTool.RequiresApproval"/>):
/// for the staff member whose access the assistant used, who must still hold one of the tool's
/// nodes, with the arguments the call was checked with. What the tool answers is the approval's result.
/// </summary>
internal sealed class McpToolApprovalHandler(McpTools tools) : ApprovalHandler<McpToolApproval>
{
    public override string Kind => McpToolApproval.KIND;

    public override async Task<ApprovalResult> ExecuteAsync(
        McpToolApproval arguments,
        Approval approval,
        CancellationToken ct
    )
    {
        if (approval.Requester.OnBehalfOf is not { } player)
            return ApprovalResult.Failed("It names no staff member to run the tool for.");

        var answer = await tools
            .RunApprovedAsync(arguments, new McpCaller(player), ct)
            .ConfigureAwait(false);
        var text = string.Join(" ", answer.Text);

        return answer.Status switch
        {
            McpToolCallStatus.Ok => ApprovalResult.Done(text),
            McpToolCallStatus.Unavailable => ApprovalResult.Failed(
                $"{arguments.Tool} is not live, or the staff member who asked may no longer call it."
            ),
            _ => ApprovalResult.Failed(text.Length > 0 ? text : $"{arguments.Tool} failed."),
        };
    }
}
