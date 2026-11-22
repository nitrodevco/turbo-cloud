using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Turbo.Admin.Commands;
using Turbo.Contracts.Hotel;
using Turbo.Database.Context;
using Turbo.Main.Hotel;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Tests.Support;

namespace Turbo.Tests.Hotel;

/// <summary>
/// The host hotel as <c>AddTurboHotel</c> builds it, over the database, providers and public room
/// editor a fixture supplies, with fakes for the rest. Every fixture that drives the real
/// <see cref="IHotel"/> builds it here, so a dependency the hotel gains is registered once for all
/// of them: the MySQL fixture once missed <see cref="IHotelAvailability"/> that the in-memory one had.
/// </summary>
internal static class HostHotelServices
{
    public static IHotel Build(
        IDbContextFactory<TurboDbContext> db,
        Fakes fakes,
        ICurrencyTypeProvider currencies,
        IFurnitureDefinitionProvider definitions,
        IPublicRoomEditor editor
    ) =>
        new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton(fakes.Create<IGrainFactory>())
            .AddSingleton(fakes.Create<ISessionGateway>())
            .AddSingleton(fakes.Create<INavigatorService>())
            .AddSingleton(fakes.Create<IRoomService>())
            .AddSingleton(fakes.Create<IPlayerNoticeService>())
            .AddSingleton(fakes.Create<ICatalogSnapshotProvider<NormalCatalog>>())
            .AddSingleton(fakes.Create<ICatalogEditService>())
            .AddSingleton(fakes.Create<IExpiringPageService>())
            .AddSingleton(fakes.Create<IHotelAvailability>())
            .AddSingleton(currencies)
            .AddSingleton(definitions)
            .AddSingleton(new TestEventBus().System)
            .AddSingleton(editor)
            .AddSingleton<AdminActionLog>()
            .AddLogging()
            .AddTurboHotel()
            .BuildServiceProvider()
            .GetRequiredService<IHotel>();
}
