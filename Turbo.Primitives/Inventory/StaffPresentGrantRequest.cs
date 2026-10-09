using Orleans;

namespace Turbo.Primitives.Inventory;

/// <summary>
/// A gift from the hotel rather than from a player, as staff send one: a piece of furni in a
/// present whose tag carries only a note, so the client shows its "Special Gift" card with no
/// sender, and optionally a badge the present gives as it is opened.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record StaffPresentGrantRequest
{
    /// <summary>The furni the present holds.</summary>
    [Id(0)]
    public required int FurniDefinitionId { get; init; }

    [Id(1)]
    public required int PresentDefinitionId { get; init; }

    [Id(2)]
    public required string Message { get; init; }

    /// <summary>A badge given when the present is opened; null for none.</summary>
    [Id(3)]
    public required string? BadgeCode { get; init; }

    /// <summary>
    /// Whether the client may drop its warning that a gift can come from anyone. Habbo's own
    /// staff gift leaves it on, so this is off unless asked for.
    /// </summary>
    [Id(4)]
    public required bool TrustedSender { get; init; }
}
