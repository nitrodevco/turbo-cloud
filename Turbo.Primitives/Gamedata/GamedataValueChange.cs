namespace Turbo.Primitives.Gamedata;

/// <summary>
/// A value to give a key that still holds <see cref="From"/>: <see cref="To"/>, or none (the key
/// goes) when that is null. A key that holds anything else is the hotel's and is left as it is.
/// </summary>
public sealed record GamedataValueChange(string From, string? To);
