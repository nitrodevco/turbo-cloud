namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What the signed-in staff member may do to players from the panel: one flag per command node,
/// and whether they may create players and issue login tickets. Each action still runs through its
/// command, which also checks they do not act on someone who outranks them.
/// </summary>
public sealed record PlayerAbilities(
    bool Ban,
    bool Unban,
    bool Silence,
    bool Tradelock,
    bool Disconnect,
    bool Warn,
    bool Alert,
    bool Give,
    bool GiveBadge,
    bool TakeBadge,
    bool GiveItem,
    bool CreatePlayers,
    bool IssueTickets,
    bool ManageAccounts
);
