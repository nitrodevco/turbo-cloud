using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

/// <summary>The chat a call for help was sent with (<c>CfhChatlogData</c>), shown as "Call For Help Evidence #id".</summary>
[GenerateSerializer, Immutable]
public sealed record CfhChatlogEventMessageComposer : IComposer
{
    [Id(0)]
    public required int CallId { get; init; }

    [Id(1)]
    public required int CallerUserId { get; init; }

    [Id(2)]
    public required int ReportedUserId { get; init; }

    [Id(3)]
    public required int ChatRecordId { get; init; }

    [Id(4)]
    public required ModChatlogRecordSnapshot Record { get; init; }
}
