using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Help;

/// <summary>
/// Refuses a call for help from a muted player (<c>HabboHelp.onCallForHelpDisabledNotify</c>): the
/// client's own <c>help.emergency.global_mute.*</c> alert, its link going to <see cref="InfoUrl"/>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CallForHelpDisabledNotifyMessageComposer : IComposer
{
    [Id(0)]
    public required string InfoUrl { get; init; }
}
