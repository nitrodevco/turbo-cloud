using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.LoadBots.Behaviour.Activities;

namespace Turbo.LoadBots.Behaviour;

/// <summary>The kind of player a bot is: what it tends to do with its time.</summary>
public enum Persona
{
    /// <summary>Simple rooms: buys a few furni, places them, chats, visits.</summary>
    Decorator,

    /// <summary>Complex rooms: draws its own floor plans, lots of furni, wired machines.</summary>
    Architect,

    /// <summary>Works through every kind of wired box, one at a time.</summary>
    WiredEngineer,

    /// <summary>Builds wired games and runs rounds of them.</summary>
    GameHost,

    /// <summary>Walks into other bots' rooms, wanders, talks and plays their games.</summary>
    Visitor,
}

/// <summary>One thing a bot can choose to do, and how much it likes doing it.</summary>
public sealed record Activity(
    string Name,
    double Weight,
    Func<BotContext, CancellationToken, Task> RunAsync
);

/// <summary>
/// What each persona does with its time. A bot picks from its list at random by weight each
/// turn, so it decides for itself while the fleet as a whole keeps a predictable mix.
/// </summary>
public static class PersonaPlans
{
    public static IReadOnlyList<Activity> For(Persona persona) =>
        persona switch
        {
            Persona.Decorator =>
            [
                new("decorate", 4, (c, ct) => BuildActivities.DecorateAsync(c, 2, 6, ct)),
                new("rearrange", 1, BuildActivities.RearrangeAsync),
                new("hang out", 2, (c, ct) => SocialActivities.HangOutAsync(c, 4, ct)),
                new("visit", 2, SocialActivities.VisitAsync),
                new("play", 1, GameActivities.PlayAsync),
            ],
            Persona.Architect =>
            [
                new("floor plan", 2, BuildActivities.DrawFloorPlanAsync),
                new("decorate", 3, (c, ct) => BuildActivities.DecorateAsync(c, 8, 20, ct)),
                new("rearrange", 2, BuildActivities.RearrangeAsync),
                new("contraption", 1, WiredActivities.BuildContraptionAsync),
                new("tidy", 1, BuildActivities.TidyAsync),
                new("visit", 1, SocialActivities.VisitAsync),
            ],
            Persona.WiredEngineer =>
            [
                new("wired lab", 6, WiredActivities.WiredLabAsync),
                new("contraption", 1, WiredActivities.BuildContraptionAsync),
                new("visit", 1, SocialActivities.VisitAsync),
            ],
            Persona.GameHost =>
            [
                new("host", 6, GameActivities.HostAsync),
                new("hang out", 1, (c, ct) => SocialActivities.HangOutAsync(c, 3, ct)),
            ],
            Persona.Visitor =>
            [
                new("visit", 5, SocialActivities.VisitAsync),
                new("play", 4, GameActivities.PlayAsync),
                new("hang out", 2, (c, ct) => SocialActivities.HangOutAsync(c, 5, ct)),
                new("decorate", 1, (c, ct) => BuildActivities.DecorateAsync(c, 1, 3, ct)),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(persona), persona, null),
        };

    public static Activity Choose(IReadOnlyList<Activity> plan, Random random)
    {
        var total = 0.0;

        foreach (var activity in plan)
            total += activity.Weight;

        var roll = random.NextDouble() * total;

        foreach (var activity in plan)
        {
            roll -= activity.Weight;

            if (roll <= 0)
                return activity;
        }

        return plan[^1];
    }
}
