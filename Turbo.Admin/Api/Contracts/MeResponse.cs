using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// Who is signed in, and which parts of the panel apply to them: while they preview a group
/// (<see cref="Preview"/>), the parts that apply to that group.
/// </summary>
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
    bool CanViewCatalog,
    bool CanViewChatlog,
    bool CanViewGamedata,
    bool CanViewSettings,
    bool CanViewContent,
    bool CanViewPlugins,
    bool CanManageRewardTracks,
    bool CanHandleReports,
    bool CanViewMessages,
    bool CanViewTelemetry,
    bool CanManageCampaigns,
    bool CanViewApprovals,
    bool CanBackupDatabase,
    PermissionPreviewView? Preview
);
