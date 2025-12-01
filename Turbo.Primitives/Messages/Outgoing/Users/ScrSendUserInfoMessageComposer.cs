using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record ScrSendUserInfoMessageComposer : IComposer
{
    [Id(0)]
    public required string ProductName { get; init; }

    [Id(1)]
    public required int DaysToPeriodEnd { get; init; }

    [Id(2)]
    public required int MemberPeriods { get; init; }

    [Id(3)]
    public required int PeriodsSubscribedAhead { get; init; }

    [Id(4)]
    public required int ResponseType { get; init; }

    [Id(5)]
    public required bool HasEverBeenMember { get; init; }

    [Id(6)]
    public required bool IsVIP { get; init; }

    [Id(7)]
    public required int PastClubDays { get; init; }

    [Id(8)]
    public required int PastVipDays { get; init; }

    [Id(9)]
    public required int MinutesUntilExpiration { get; init; }

    [Id(10)]
    public required int MinutesSinceLastModified { get; init; }
}
