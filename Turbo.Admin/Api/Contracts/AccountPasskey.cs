using System;

namespace Turbo.Admin.Api.Contracts;

public sealed record AccountPasskey(
    int Id,
    string Name,
    DateTime CreatedAtUtc,
    DateTime? LastUsedAtUtc
);
