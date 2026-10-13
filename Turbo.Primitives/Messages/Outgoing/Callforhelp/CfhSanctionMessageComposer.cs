using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Callforhelp;

/// <summary>
/// The default sanction a player would get (<c>CfhSanctionMessageParser</c>): the issue (-1 for an
/// account), the player, then <c>CfhSanctionTypeData</c> - name, hours, type id, avatar only and the
/// optional trade lock and machine ban texts, which <c>IssueManager.updateSanctionData</c> joins
/// into the issue handler's sanction text.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CfhSanctionMessageComposer : IComposer
{
    [Id(0)]
    public required int IssueId { get; init; }

    [Id(1)]
    public required int AccountId { get; init; }

    [Id(2)]
    public required SanctionTypeSnapshot Sanction { get; init; }
}
