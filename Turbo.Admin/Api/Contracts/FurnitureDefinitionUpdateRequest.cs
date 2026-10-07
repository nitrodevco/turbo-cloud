using System.Text.Json.Nodes;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A definition's furnidata fields to change, by furnidata key (<c>name</c>, <c>xdim</c>, ...).</summary>
public sealed record FurnitureDefinitionUpdateRequest(JsonObject? Fields);
