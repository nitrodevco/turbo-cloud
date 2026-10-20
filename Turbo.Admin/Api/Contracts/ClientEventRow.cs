using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A raw logged event.</summary>
public sealed record ClientEventRow(
    DateTime At,
    int? RoomId,
    string? RoomName,
    string Category,
    string Type,
    string Action,
    string ExtraString,
    int ExtraInt
);
