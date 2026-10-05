namespace Turbo.Web.Api.Contracts;

/// <summary>
/// Who is here: the signed-in player and any ban on them; or someone signing up, between Discord
/// and choosing a name; or nobody, when both are null.
/// </summary>
public sealed record MeResponse(WebPlayer? Player, BanInfo? Ban, SignUpInfo? SignUp);
