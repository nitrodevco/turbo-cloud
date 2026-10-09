using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Callforhelp;

/// <summary>The calls for help the player asking has made.</summary>
[GenerateSerializer, Immutable]
public sealed record MyCfhReportStatusMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<CfhReportStatusSnapshot> Reports { get; init; }
}
