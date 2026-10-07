using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Admin.Api.Contracts;

/// <summary>Figure records of one kind to set and keys to remove, as one change set.</summary>
public sealed record FigureBatchRequest(
    FigureRecordKind Kind,
    string[]? Save,
    string[]? Delete,
    string? Summary
);
