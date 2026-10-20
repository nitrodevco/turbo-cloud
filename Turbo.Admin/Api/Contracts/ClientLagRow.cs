using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A raw chat lag report.</summary>
public sealed record ClientLagRow(
    DateTime At,
    int PlayerId,
    string? PlayerName,
    int? RoomId,
    string? RoomName,
    int Warnings
);
