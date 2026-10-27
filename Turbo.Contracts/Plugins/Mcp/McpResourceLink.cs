namespace Turbo.Contracts.Plugins.Mcp;

/// <summary>
/// A pointer to a resource the assistant can read (<c>resources/read</c>), sent as MCP's
/// <c>resource_link</c> content: in a tool's answer (<see cref="McpToolResult.WithLinks"/>) or a
/// prompt's message (<see cref="McpPromptMessage.Link"/>). The assistant reads it only if it wants
/// to, and only as the staff member it acts for may.
/// </summary>
/// <param name="Uri">The resource's address, such as <c>turbo://rooms/42</c>.</param>
/// <param name="Name">A short name for it.</param>
/// <param name="Title">What people read; the name when null.</param>
/// <param name="Description">What it holds.</param>
/// <param name="MimeType">Its type when known, such as <c>application/json</c>.</param>
public sealed record McpResourceLink(
    string Uri,
    string Name,
    string? Title = null,
    string? Description = null,
    string? MimeType = null
);
