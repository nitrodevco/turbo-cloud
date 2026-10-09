using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record GiftArguments(
    [Selectors(PermissionNodes.Command.GIVE_MASS)] PlayerTarget Who,
    [Suggest(SuggestionSources.FURNI)] string Furni,
    [CommandParameter(Description = "The note on the tag, in double quotes")] string Message,
    [CommandParameter(Description = "A badge given as it is opened")] string? Badge = null,
    [CommandParameter(Description = "Drop the client's warning about the sender")]
        bool Trusted = false
);

/// <summary>
/// <c>:gift name furni_name "message" [BADGE] [trusted]</c>. Sends a player a present from the
/// hotel, as Habbo's staff send theirs ("Thanks for playing Habbo. Enjoy the dragon lamp and the
/// badge!"): the tag names no sender, so the client shows its "Special Gift" card with no face,
/// and the badge, if any, is given as the present is opened. The client still warns that a gift
/// can come from anyone unless <c>trusted</c> is given; Habbo's own staff gift leaves it on. The
/// present is one of the gift wrapped boxes the catalog offers, so it opens with their burst.
/// <c>@room</c> and <c>@online</c> need <c>command.give.mass</c> and are always logged.
/// </summary>
[Command(
    "gift",
    Description = "Send a player a present from the hotel",
    Category = CommandCategories.SUPPORT
)]
[RequiresPermission(PermissionNodes.Command.GIFT)]
public sealed class GiftCommand(
    IGrainFactory grainFactory,
    IFurnitureDefinitionProvider definitionProvider,
    IGiftWrappingProvider giftWrappingProvider,
    IOptions<OperationsConfig> config
) : IOperatorCommand<GiftArguments>
{
    private const string SENT = "sent";
    private const string SENT_MANY = "sent_many";
    private const string FAILED = "failed";
    private const string UNKNOWN_FURNI = "unknown_furni";
    private const string NOT_GIFTABLE = "not_giftable";
    private const string NO_WRAPPING = "no_wrapping";
    private const string BAD_MESSAGE = "bad_message";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [SENT] = "%0% was sent a present holding %1%.",
            [SENT_MANY] = "%0% players were sent a present holding %1%.",
            [FAILED] = "%0% could not be sent a present holding %1%.",
            [UNKNOWN_FURNI] = "There is no furniture called %0%.",
            [NOT_GIFTABLE] = "%0% cannot go in a present.",
            [NO_WRAPPING] =
                "There is no present box to wrap it in; check the gift wrapping config.",
            [BAD_MESSAGE] = "The note must be between 1 and %0% characters.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        GiftArguments arguments,
        CancellationToken ct
    )
    {
        var maxLength = config.Value.MaxGiftMessageLength;
        var message = arguments.Message.Trim();

        if (message.Length < 1 || message.Length > maxLength)
            return CommandResult.Fail(
                BAD_MESSAGE,
                maxLength.ToString(CultureInfo.InvariantCulture)
            );

        if (FindDefinition(arguments.Furni) is not { } content)
            return CommandResult.Fail(UNKNOWN_FURNI, arguments.Furni);

        // A present opens into one row; a teleporter is a pair and would lose its other half.
        if (
            content.ProductType is not (ProductType.Floor or ProductType.Wall)
            || TeleportFurniture.IsLinkedPair(content.LogicName, content.Name)
        )
            return CommandResult.Fail(NOT_GIFTABLE, content.Name);

        if (PickPresent() is not { } present)
            return CommandResult.Fail(NO_WRAPPING);

        var selection = await ctx.SelectAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var request = new StaffPresentGrantRequest
        {
            FurniDefinitionId = content.Id,
            PresentDefinitionId = present.Id,
            Message = message,
            BadgeCode = string.IsNullOrWhiteSpace(arguments.Badge) ? null : arguments.Badge.Trim(),
            TrustedSender = arguments.Trusted,
        };

        var batch = await ctx.ExecuteBatchAsync(
            selection.Players,
            1,
            async (player, token) =>
            {
                await grainFactory
                    .GetInventoryGrain(player.Id)
                    .ReceiveStaffPresentAsync(request, token);

                return true;
            },
            ct
        );

        var completed = selection.IsSelector
            ? CommandResult.Done(
                SENT_MANY,
                batch.CompletedTargets.ToString(CultureInfo.InvariantCulture),
                content.Name
            )
            : CommandResult.Done(SENT, selection.Players[0].Name, content.Name);

        return batch.ToResult(
            completed,
            selection.IsSelector
                ? default
                : CommandResult.Fail(FAILED, selection.Players[0].Name, content.Name)
        );
    }

    /// <summary>The furni by its definition name, or by its definition id.</summary>
    private FurnitureDefinitionSnapshot? FindDefinition(string furni) =>
        definitionProvider.TryGetDefinitionByName(furni)
        ?? (
            int.TryParse(furni, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? definitionProvider.TryGetDefinition(id)
                : null
        );

    /// <summary>
    /// One of the gift wrapped boxes the catalog offers, at random as the client picks a free
    /// box, or a free box when none is configured.
    /// </summary>
    private FurnitureDefinitionSnapshot? PickPresent()
    {
        var wrapping = giftWrappingProvider.GetWrapping();
        var presents = Presents(wrapping.StuffTypes);

        if (presents.Count == 0)
            presents = Presents(wrapping.DefaultStuffTypes);

        return presents.Count == 0 ? null : presents[Random.Shared.Next(presents.Count)];
    }

    private List<FurnitureDefinitionSnapshot> Presents(ImmutableArray<int> spriteIds) =>
        [
            .. spriteIds
                .Select(x => definitionProvider.TryGetDefinitionBySprite(ProductType.Floor, x))
                .OfType<FurnitureDefinitionSnapshot>()
                .Where(x => PresentData.IsPresent(x.LogicName)),
        ];
}
