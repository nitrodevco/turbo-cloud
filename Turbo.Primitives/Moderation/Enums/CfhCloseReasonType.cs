namespace Turbo.Primitives.Moderation.Enums;

/// <summary>
/// Why a moderator closed a call for help: the issue handler's three close buttons
/// (<c>IssueManager.RESOLUTION_USELESS</c> 1, abusive 2, <c>RESOLUTION_RESOLVED</c> 3), which the
/// reporter's <c>IssueCloseNotification</c> turns into <c>help.cfh.closed.useless</c> /
/// <c>abusive</c> / <c>resolved</c> (<c>CallForHelpManager.getCloseReasonKey</c>).
/// </summary>
public enum CfhCloseReasonType : byte
{
    Useless = 1,
    Abusive = 2,
    Resolved = 3,
}
