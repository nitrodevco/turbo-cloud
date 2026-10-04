namespace Turbo.Admin.Api.Contracts;

public sealed record DashboardRoom(
    int Id,
    string Name,
    string OwnerName,
    int Population,
    int PlayersMax
);
