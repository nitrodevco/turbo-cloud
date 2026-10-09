namespace Turbo.Primitives.Moderation.Enums;

/// <summary>
/// What became of a call for help, as its <c>CallForHelpResult</c> says. The client only shows
/// the message (or <c>help.cfh.sent.text</c> when there is none) and never reads the type.
/// </summary>
public enum CfhResultType
{
    Sent = 0,
    Refused = 1,
}
