namespace Turbo.Web.Api.Contracts;

/// <summary>The signed-in player: who they are, and a picture of their look when the hotel has one.</summary>
public sealed record WebPlayer(
    int Id,
    string Name,
    string? Motto,
    string Figure,
    string? AvatarUrl
);
