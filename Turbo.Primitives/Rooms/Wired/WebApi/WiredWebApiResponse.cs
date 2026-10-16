using Orleans;

namespace Turbo.Primitives.Rooms.Wired.WebApi;

/// <summary>The HTTP status and the JSON body (none for a 204) a Web API call is answered with.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredWebApiResponse(
    [property: Id(0)] int Status,
    [property: Id(1)] string? Json
);
