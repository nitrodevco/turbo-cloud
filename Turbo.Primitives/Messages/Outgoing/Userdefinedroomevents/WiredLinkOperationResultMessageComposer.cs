using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Wired.Link;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// The answer to every WiredLinkRequestMessage. The client acts on it only for the linker its
/// link window was opened for; a failure shows <c>wiredlink.error.&lt;ResultCode&gt;</c>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredLinkOperationResultMessageComposer : IComposer
{
    [Id(0)]
    public required int RoomId { get; init; }

    [Id(1)]
    public required int FurniId { get; init; }

    /// <summary>A <see cref="WiredLinkOperations"/> value.</summary>
    [Id(2)]
    public required int Operation { get; init; }

    /// <summary>A <see cref="WiredLinkResultCodes"/> value.</summary>
    [Id(3)]
    public required int ResultCode { get; init; }

    /// <summary>The linker's network after the request; null leaves the client's as it is.</summary>
    [Id(4)]
    public string? SelectedNetworkId { get; init; }

    [Id(5)]
    public bool ReceiveLocal { get; init; }

    [Id(6)]
    public string RemovedNetworkId { get; init; } = "0";

    [Id(7)]
    public ImmutableArray<WiredLinkNetworkSnapshot> Networks { get; init; } = [];
}
