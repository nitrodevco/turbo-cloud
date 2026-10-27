using System.Text.Json;

namespace Turbo.Contracts.Plugins.Mcp;

/// <summary>
/// The arguments of an approval an MCP tool asked for (<see cref="IMcpTool.RequiresApproval"/>):
/// which tool, and the arguments it was called with, checked against its schema. Its kind is
/// <see cref="KIND"/>; the player it acts for is the staff member whose access the assistant used.
/// </summary>
public sealed record McpToolApproval(string Tool, JsonElement Arguments)
{
    /// <summary>The approval kind of every MCP tool's call.</summary>
    public const string KIND = "mcp.tool";
}
