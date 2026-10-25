using System;

namespace Turbo.Primitives.Games;

/// <summary>
/// Keeps a public <see cref="IRoomGame"/>, <see cref="IGameRewardHandler"/> or
/// <see cref="IGameAccessPolicy"/> out of the game centre's assembly scan: the plugin builds a game
/// or reward handler and registers it itself through <see cref="IGameRegistry.Register"/> (for
/// example only when its settings turn the game on, or with a definition read from them), and an
/// access policy by registering it in its services (<c>docs/plugin-services.md</c>). See
/// <c>docs/plugin-games.md</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ManualGameRegistrationAttribute : Attribute;
