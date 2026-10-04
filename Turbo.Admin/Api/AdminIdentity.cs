using System;
using Microsoft.AspNetCore.Http;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Api;

/// <summary>The staff member behind a request, as <see cref="AdminSessionFilter"/> found them.</summary>
internal sealed record AdminIdentity(
    PlayerId PlayerId,
    string Name,
    string SessionToken,
    DateTime ExpiresAtUtc
)
{
    private const string ITEM_KEY = "turbo.admin.identity";

    public static AdminIdentity Of(HttpContext http) =>
        http.Items[ITEM_KEY] as AdminIdentity
        ?? throw new InvalidOperationException(
            "The request did not pass the admin session filter."
        );

    public void AttachTo(HttpContext http) => http.Items[ITEM_KEY] = this;
}
