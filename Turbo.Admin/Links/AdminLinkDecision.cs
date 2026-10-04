namespace Turbo.Admin.Links;

/// <summary>
/// Whether a setup link may be made, and whether using it would replace passkeys the player
/// already has.
/// </summary>
public sealed record AdminLinkDecision(AdminLinkRefusal Refusal, bool ReplacesExisting)
{
    public bool IsAllowed => Refusal == AdminLinkRefusal.None;
}
