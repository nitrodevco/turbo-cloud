namespace Turbo.Admin.Api.Contracts;

/// <summary>A product to set: its code - an offer's name key - and the name and description the client shows.</summary>
public sealed record ProductSaveRequest(string? Code, string? Name, string? Description);
