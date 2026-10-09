using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Vouchers: codes players redeem in the catalogue. For staff with <c>admin.catalog.view</c>, as
/// the catalogue is; making, changing and removing them needs <c>catalog.manage</c> as well.
/// </summary>
internal sealed class VoucherEndpoints(
    IGrainFactory grainFactory,
    IVoucherService vouchers,
    IFurnitureDefinitionProvider definitions,
    ILogger<VoucherEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't see the vouchers.";
    private const string NO_MANAGE = "You can't change the vouchers.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/vouchers").AddEndpointFilter(RequireViewAsync);

        group.MapGet("/", SearchAsync);
        group.MapPost(
            "/",
            (VoucherRequest request, HttpContext http, CancellationToken ct) =>
                SaveAsync(0, request, http, ct)
        );
        group.MapPost("/generate", GenerateAsync);
        group.MapPut("/{id:int}", SaveAsync);
        group.MapDelete(
            "/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        await vouchers.DeleteAsync(id, ct).ConfigureAwait(false)
                            ? Results.NoContent()
                            : Results.NotFound()
                )
        );
        group.MapGet(
            "/{id:int}/redemptions",
            async (int id, CancellationToken ct) =>
                Results.Ok(
                    new
                    {
                        redemptions = await vouchers
                            .GetRedemptionsAsync(id, ct)
                            .ConfigureAwait(false),
                    }
                )
        );
    }

    private async Task<IResult> SearchAsync(
        string? q,
        int? page,
        HttpContext http,
        CancellationToken ct
    )
    {
        var (found, total, pageSize) = await vouchers
            .SearchAsync(q, page ?? 0, ct)
            .ConfigureAwait(false);

        return Results.Ok(
            new
            {
                vouchers = found.Select(Item),
                total,
                pageSize,
                canManage = await CanManageAsync(http, ct).ConfigureAwait(false),
            }
        );
    }

    private Task<IResult> SaveAsync(
        int id,
        VoucherRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                var saved = await vouchers
                    .SaveAsync(Snapshot(id, request), ct)
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "Staff {StaffId} saved voucher {VoucherId} ({Code})",
                    AdminIdentity.Of(http).PlayerId.Value,
                    saved.Id,
                    saved.Code
                );

                return Results.Ok(Item(saved));
            }
        );

    private Task<IResult> GenerateAsync(
        VoucherGenerateRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                var made = await vouchers
                    .GenerateAsync(
                        Snapshot(0, request.Voucher ?? new VoucherRequest()),
                        request.Count ?? 0,
                        request.Prefix ?? string.Empty,
                        ct
                    )
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "Staff {StaffId} made {Count} vouchers",
                    AdminIdentity.Of(http).PlayerId.Value,
                    made.Length
                );

                return Results.Ok(new { vouchers = made.Select(Item) });
            }
        );

    private VoucherItem Item(VoucherSnapshot voucher) =>
        new(
            voucher,
            voucher.FurnitureDefinitionId is { } id ? definitions.TryGetDefinition(id)?.Name : null
        );

    private static VoucherSnapshot Snapshot(int id, VoucherRequest request) =>
        new()
        {
            Id = id,
            Code = request.Code ?? string.Empty,
            Credits = request.Credits ?? 0,
            CurrencyTypeId = request.CurrencyTypeId,
            CurrencyAmount = request.CurrencyAmount ?? 0,
            FurnitureDefinitionId = request.FurnitureDefinitionId,
            FurnitureQuantity = request.FurnitureQuantity ?? 0,
            BadgeCode = request.BadgeCode,
            MaxUses = request.MaxUses,
            Uses = 0,
            ExpiresAt = request.ExpiresAt is { } t
                ? t.Kind == DateTimeKind.Local
                    ? t.ToUniversalTime()
                    : DateTime.SpecifyKind(t, DateTimeKind.Utc)
                : null,
            Enabled = request.Enabled ?? true,
            Note = request.Note ?? string.Empty,
            CreatedAt = default,
        };

    private async Task<IResult> ManageAsync(
        HttpContext http,
        CancellationToken ct,
        Func<Task<IResult>> change
    )
    {
        if (!await CanManageAsync(http, ct).ConfigureAwait(false))
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

        try
        {
            return await change().ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private Task<bool> CanManageAsync(HttpContext http, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(
            AdminIdentity.Of(http).PlayerId,
            PermissionNodes.Catalog.MANAGE,
            ct
        );

    private async ValueTask<object?> RequireViewAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.CATALOG_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
