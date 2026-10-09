using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Plugins;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Texts;

namespace Turbo.Operations.Commands;

public enum ReloadSubject
{
    Catalog,
    Texts,
    Furni,
    Navigator,
    Currencies,
    ChatStyles,
    RoomModels,
    PetBreeds,
    Achievements,
    Plugins,
    Filter,
    PetSpeech,
}

public sealed record ReloadArguments(ReloadSubject Subject);

/// <summary>
/// <c>:reload subject</c>. Has the hotel read one thing it caches from the database again, after
/// a hand edit: one command with a subject, not one command per cache. A subject is here only
/// because something holds it in memory; a reload that fails keeps what was loaded, as each
/// provider does for itself.
/// </summary>
[Command(
    "reload",
    Description = "Read the catalog, texts, furniture, filter words and the like from the database again",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.RELOAD)]
public sealed class ReloadCommand(
    ICatalogSnapshotProvider<NormalCatalog> normalCatalog,
    ICatalogSnapshotProvider<BuildersClubCatalog> buildersClubCatalog,
    IHotelTextProvider textProvider,
    IFurnitureDefinitionProvider furnitureDefinitionProvider,
    INavigatorProvider navigatorProvider,
    ICurrencyTypeProvider currencyTypeProvider,
    IChatStyleProvider chatStyleProvider,
    IRoomModelProvider roomModelProvider,
    IPetBreedProvider petBreedProvider,
    IPetSpeechProvider petSpeechProvider,
    IAchievementCatalog achievements,
    IWordFilter wordFilter,
    PluginManager pluginManager
) : IOperatorCommand<ReloadArguments>
{
    private const string RELOADED = "reloaded";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string> { [RELOADED] = "Reloaded %0%." };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        ReloadArguments arguments,
        CancellationToken ct
    )
    {
        switch (arguments.Subject)
        {
            case ReloadSubject.Catalog:
                await normalCatalog.ReloadAsync(ct);
                await buildersClubCatalog.ReloadAsync(ct);

                break;
            case ReloadSubject.Texts:
                // Texts are read from the database as they are asked for; this forgets those read.
                textProvider.Invalidate();

                break;
            case ReloadSubject.Furni:
                await furnitureDefinitionProvider.ReloadAsync(ct);

                break;
            case ReloadSubject.Navigator:
                await navigatorProvider.ReloadAsync(ct);

                break;
            case ReloadSubject.Currencies:
                await currencyTypeProvider.ReloadAsync(ct);

                break;
            case ReloadSubject.ChatStyles:
                await chatStyleProvider.ReloadAsync(ct);

                break;
            case ReloadSubject.RoomModels:
                await roomModelProvider.ReloadAsync(ct);

                break;
            case ReloadSubject.PetBreeds:
                await petBreedProvider.ReloadAsync(ct);

                break;
            case ReloadSubject.PetSpeech:
                await petSpeechProvider.ReloadAsync(ct);

                break;
            case ReloadSubject.Plugins:
                await pluginManager.LoadAllAsync(true, ct);

                break;
            case ReloadSubject.Achievements:
                await achievements.ReloadAsync(ct);
                break;
            case ReloadSubject.Filter:
                await wordFilter.ReloadAsync(ct);

                break;
        }

        return CommandResult.Done(RELOADED, arguments.Subject.ToString().ToLowerInvariant());
    }
}
