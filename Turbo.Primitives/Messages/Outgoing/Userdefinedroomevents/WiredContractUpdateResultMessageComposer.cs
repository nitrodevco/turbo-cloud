using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// The answer to WiredUpdateContractMessage: success closes the editor, a failure shows
/// <c>wiredcontracts.error.&lt;FailCode&gt;</c>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredContractUpdateResultMessageComposer : IComposer
{
    [Id(0)]
    public required int ContractId { get; init; }

    [Id(1)]
    public required bool IsSuccess { get; init; }

    /// <summary>One of <c>WiredContractFailCodes</c>, or an empty string on success.</summary>
    [Id(2)]
    public required string FailCode { get; init; }
}
