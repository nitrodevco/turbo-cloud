using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Turbo.Admin.Api.Contracts;

namespace Turbo.Admin.Api;

/// <summary>Answers shared by the endpoints.</summary>
internal static class AdminResults
{
    public const string SIGN_IN_POLICY = "sign-in";

    /// <summary>
    /// A passkey ceremony for the browser: its id, and the WebAuthn options exactly as the Fido2
    /// library serialized them (base64url binary, the spec's own names), for
    /// <c>PublicKeyCredential.parseCreationOptionsFromJSON</c> and friends.
    /// </summary>
    public static IResult Ceremony(string ceremonyId, string optionsJson) =>
        Results.Text(
            new JsonObject
            {
                ["ceremonyId"] = ceremonyId,
                ["options"] = JsonNode.Parse(optionsJson),
            }.ToJsonString(),
            "application/json"
        );

    public static IResult Error(int status, string message) =>
        Results.Json(new ErrorResponse(message), statusCode: status);
}
