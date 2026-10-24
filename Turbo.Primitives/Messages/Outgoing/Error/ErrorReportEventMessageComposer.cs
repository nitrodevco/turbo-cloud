using System;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Error;

/// <summary>
/// A request the server failed on. The client shows "Server error" with the code
/// (<c>HabboCommunicationDemo.handleErrorMessage</c>: <c>${connection.server.error.desc}</c>, "Sorry,
/// received server error: %errorCode%"), closes the connection for 1001 to 1019 and shows the room
/// maintenance alert for 4013; it reads but never uses the message id.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ErrorReportEventMessageComposer : IComposer
{
    /// <summary>Which request failed, by Habbo's own number for it, not a header (3051 for a wired action save).</summary>
    [Id(0)]
    public required int MessageId { get; init; }

    [Id(1)]
    public required int ErrorCode { get; init; }

    /// <summary>When it failed; written as Habbo does, "10/24/26, 7:40 AM".</summary>
    [Id(2)]
    public required DateTimeOffset Time { get; init; }
}
