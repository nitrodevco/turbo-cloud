namespace Turbo.Authorization.Players.Contexts;

public sealed record PlayerLoginContext(long PlayerId, bool IsBanned);
