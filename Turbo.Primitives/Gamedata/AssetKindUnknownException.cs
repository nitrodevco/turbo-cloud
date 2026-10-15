using System;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// An upload whose content says neither which kind of bundle it is nor which gamedata file: a
/// library with no furniture, pet, effect or clothing in it, or a file of something else.
/// </summary>
public sealed class AssetKindUnknownException(string message) : ArgumentException(message);
