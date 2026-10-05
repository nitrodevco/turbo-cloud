using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>Who is signed in, and which parts of the panel apply to them.</summary>
public sealed record MeResponse(
    int Id,
    string Name,
    DateTime SessionExpiresAtUtc,
    bool CanManagePermissions,
    bool CanResetPasskeys,
    bool CanViewRooms,
    bool CanViewPermissions,
    bool CanViewPlayers,
    bool CanViewCommandLog,
    bool CanViewCatalog
);
