using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record ApproveNameMessageComposer : IComposer
{
    [Id(0)]
    public required PetNameValidationType Result { get; init; }

    /// <summary>
    /// Fills the client's <c>additional_info</c> text: the length limit that was broken, or empty
    /// for the plain message.
    /// </summary>
    [Id(1)]
    public required string NameValidationInfo { get; init; }
}
