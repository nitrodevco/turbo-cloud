using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Web.Configuration;

namespace Turbo.Web.Discord;

/// <summary>
/// Discord's OAuth2 authorization-code flow, asking only for <c>identify</c>: the address that asks
/// the person to allow it, and turning the code Discord sends back into who they are. The access
/// token is used for that one question and kept nowhere.
/// </summary>
public sealed class DiscordOAuthClient(
    HttpClient http,
    IOptions<WebConfig> config,
    ILogger<DiscordOAuthClient> logger
)
{
    public const string HTTP_CLIENT = "discord";
    private const string SCOPE = "identify";

    private WebConfig Web => config.Value;

    public string RedirectUri => $"{Web.SiteUrl.TrimEnd('/')}/api/auth/discord/callback";

    public string AuthorizeUrl(string state) =>
        $"{Web.Discord.AuthorizeUrl}?"
        + string.Join(
            '&',
            new Dictionary<string, string>
            {
                ["response_type"] = "code",
                ["client_id"] = Web.Discord.ClientId,
                ["scope"] = SCOPE,
                ["state"] = state,
                ["redirect_uri"] = RedirectUri,
                ["prompt"] = "none",
            }.Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}")
        );

    /// <summary>Who the code is for; null when Discord would not say.</summary>
    public async Task<DiscordUser?> GetUserAsync(string code, CancellationToken ct)
    {
        try
        {
            using var exchange = await http.PostAsync(
                    $"{Web.Discord.ApiUrl.TrimEnd('/')}/oauth2/token",
                    new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            ["grant_type"] = "authorization_code",
                            ["code"] = code,
                            ["redirect_uri"] = RedirectUri,
                            ["client_id"] = Web.Discord.ClientId,
                            ["client_secret"] = Web.Discord.ClientSecret,
                        }
                    ),
                    ct
                )
                .ConfigureAwait(false);

            if (!exchange.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Discord refused a sign-in code ({Status})",
                    (int)exchange.StatusCode
                );

                return null;
            }

            var token = await exchange
                .Content.ReadFromJsonAsync<TokenResponse>(ct)
                .ConfigureAwait(false);

            if (token?.AccessToken is not { Length: > 0 } accessToken)
                return null;

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{Web.Discord.ApiUrl.TrimEnd('/')}/users/@me"
            );

            request.Headers.Authorization = new("Bearer", accessToken);

            using var me = await http.SendAsync(request, ct).ConfigureAwait(false);

            if (!me.IsSuccessStatusCode)
                return null;

            var user = await me.Content.ReadFromJsonAsync<UserResponse>(ct).ConfigureAwait(false);

            return user is { Id.Length: > 0, Username.Length: > 0 }
                ? new DiscordUser(user.Id, user.Username, user.GlobalName)
                : null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Could not reach Discord to finish a sign-in");

            return null;
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken
    );

    private sealed record UserResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("global_name")] string? GlobalName
    );
}
