namespace Turbo.Primitives.Players.Accounts;

/// <summary>The player created, by id; or why not, in words.</summary>
public sealed record NewPlayerResult(PlayerId? Created, string? Error)
{
    public static NewPlayerResult Done(PlayerId id) => new(id, null);

    public static NewPlayerResult Refused(string error) => new(null, error);
}
