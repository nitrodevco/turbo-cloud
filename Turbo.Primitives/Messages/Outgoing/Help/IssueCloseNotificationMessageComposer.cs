using Orleans;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Help;

/// <summary>
/// Tells a reporter their call for help was closed (<c>CallForHelpManager.onIssueClose</c>): an
/// empty text is the client's own <c>help.cfh.closed.useless</c> / <c>abusive</c> / <c>resolved</c>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record IssueCloseNotificationMessageComposer : IComposer
{
    [Id(0)]
    public required CfhCloseReasonType CloseReason { get; init; }

    [Id(1)]
    public required string MessageText { get; init; }
}
