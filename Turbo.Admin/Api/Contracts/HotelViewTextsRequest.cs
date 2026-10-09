using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>The keys of the texts the hotel view's widgets show.</summary>
public sealed record HotelViewTextsRequest(List<string>? Keys);
