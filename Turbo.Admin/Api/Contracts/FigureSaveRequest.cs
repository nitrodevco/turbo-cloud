using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A figure record to set: its kind and its fields as JSON, by the names Habbo's file gives them.</summary>
public sealed record FigureSaveRequest(FigureRecordKind Kind, string? Data);
