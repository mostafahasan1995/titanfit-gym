using System.Security.Claims;
using Gym.Api.Auth;
using Gym.Api.Live;
using Gym.Infrastructure.Storage;

namespace Gym.Api.Endpoints;

internal static class MemberEndpoints
{
    public static void MapMemberEndpoints(this RouteGroupBuilder api)
    {
        var members = api.MapGroup("/members").WithTags("Members").RequireAuthorization(Policies.Reception);

        // q matches a QR code exactly, or a name (Arabic-normalized), phone, national number or member number.
        members.MapGet("/", (string? q, SubscriptionStatus? status, MemberType? type, int? page, int? pageSize, MemberService service, CancellationToken ct) =>
            service.SearchAsync(q, status, type, page ?? 1, pageSize ?? 20, ct));

        members.MapGet("/{id:guid}", (Guid id, MemberService service, CancellationToken ct) => service.GetAsync(id, ct));

        members.MapPost("/", async (CreateMemberRequest request, ClaimsPrincipal user, MemberService service, LiveUpdates live, CancellationToken ct) =>
        {
            var member = await service.CreateAsync(request, user.StaffId(), ct);
            await live.MemberChangedAsync(member.Id, MemberChangeKind.Created);
            return Results.Created($"/api/members/{member.Id}", member);
        });

        members.MapPut("/{id:guid}", async (Guid id, UpdateMemberRequest request, MemberService service, LiveUpdates live, CancellationToken ct) =>
        {
            var member = await service.UpdateAsync(id, request, ct);
            await live.MemberChangedAsync(id, MemberChangeKind.Updated);
            return member;
        });

        members.MapPost("/{id:guid}/renew", async (Guid id, RenewRequest request, ClaimsPrincipal user, MemberService service, LiveUpdates live, CancellationToken ct) =>
        {
            var subscription = await service.RenewAsync(id, request.PlanId, user.StaffId(), ct);
            await live.MemberChangedAsync(id, MemberChangeKind.Renewed);
            return subscription;
        });

        members.MapGet("/{id:guid}/subscriptions", (Guid id, MemberService service, CancellationToken ct) => service.GetSubscriptionsAsync(id, ct));

        // A new random code; the old QR stops working at once.
        members.MapPost("/{id:guid}/qr-code", async (Guid id, MemberService service, LiveUpdates live, CancellationToken ct) =>
        {
            var code = await service.ReissueQrCodeAsync(id, ct);
            await live.MemberChangedAsync(id, MemberChangeKind.QrCodeReissued);
            return code;
        });

        // Body: the image bytes with Content-Type image/jpeg or image/png (max 5 MB).
        members.MapPut("/{id:guid}/photo", async (Guid id, HttpRequest request, MemberService service, PhotoStore photos, LiveUpdates live, CancellationToken ct) =>
        {
            var extension = PhotoStore.ExtensionFor(request.ContentType)
                ?? throw new DomainException("unsupported_photo_type", "Send the photo as image/jpeg or image/png.");
            if (request.ContentLength > PhotoStore.MaxBytes)
            {
                throw new DomainException("photo_too_large", "The photo can be at most 5 MB.");
            }

            await service.EnsureExistsAsync(id, ct);
            var file = await photos.SaveAsync(id, request.Body, extension, ct);
            photos.Delete(await service.SetPhotoAsync(id, file, ct));
            await live.MemberChangedAsync(id, MemberChangeKind.PhotoChanged);
            return Results.NoContent();
        });

        members.MapGet("/{id:guid}/photo", async (Guid id, MemberService service, PhotoStore photos, CancellationToken ct) =>
        {
            var path = photos.GetPath(await service.GetPhotoFileAsync(id, ct));
            return path is null ? Results.NotFound() : Results.File(path, PhotoStore.ContentTypeFor(path));
        });
    }

    public static void MapCheckInEndpoints(this RouteGroupBuilder api)
    {
        var checkIns = api.MapGroup("/check-ins").WithTags("Check-in").RequireAuthorization(Policies.Reception);

        // Give either the scanned QR code or the typed member number. Every scan is logged.
        checkIns.MapPost("/", async (CheckInRequest request, ClaimsPrincipal user, CheckInService service, LiveUpdates live, CancellationToken ct) =>
        {
            var result = await service.CheckInAsync(request, user.StaffId(), ct);
            await live.CheckInAsync(result);
            return result;
        });

        checkIns.MapGet("/", (DateOnly? from, DateOnly? to, Guid? memberId, CheckInService service, GymClock clock, CancellationToken ct) =>
            service.ListAsync(from ?? clock.Today, to ?? from ?? clock.Today, memberId, ct));
    }

    public static void MapPrivateTrainerEndpoints(this RouteGroupBuilder api)
    {
        var trainers = api.MapGroup("/private-trainers").WithTags("Private trainers").RequireAuthorization(Policies.Reception);

        trainers.MapGet("/", (MemberService service, CancellationToken ct) => service.ListPrivateTrainersAsync(ct));

        trainers.MapGet("/{id:guid}/clients", (Guid id, MemberService service, CancellationToken ct) => service.ListClientsAsync(id, ct));

        trainers.MapPut("/{id:guid}/clients/{memberId:guid}", async (Guid id, Guid memberId, MemberService service, LiveUpdates live, CancellationToken ct) =>
        {
            await service.LinkClientAsync(id, memberId, ct);
            await live.MemberChangedAsync(memberId, MemberChangeKind.TrainerLinkChanged);
            return Results.NoContent();
        });

        trainers.MapDelete("/{id:guid}/clients/{memberId:guid}", async (Guid id, Guid memberId, MemberService service, LiveUpdates live, CancellationToken ct) =>
        {
            await service.UnlinkClientAsync(id, memberId, ct);
            await live.MemberChangedAsync(memberId, MemberChangeKind.TrainerLinkChanged);
            return Results.NoContent();
        });

        // Buy or renew the private training service: months × the monthly fee from the settings.
        trainers.MapPost("/{id:guid}/service", async (Guid id, PrivateServiceRequest request, ClaimsPrincipal user, MemberService service, LiveUpdates live, CancellationToken ct) =>
        {
            var subscription = await service.AddPrivateServiceAsync(id, request.Months, user.StaffId(), ct);
            await live.MemberChangedAsync(id, MemberChangeKind.PrivateServiceChanged);
            return subscription;
        });
    }

    public static void MapPlanEndpoints(this RouteGroupBuilder api)
    {
        var plans = api.MapGroup("/plans").WithTags("Plans");

        plans.MapGet("/", (bool? includeInactive, PlanService service, CancellationToken ct) => service.ListAsync(includeInactive ?? false, ct))
            .RequireAuthorization(Policies.AnyStaff);

        plans.MapPost("/", async (SavePlanRequest request, PlanService service, CancellationToken ct) =>
        {
            var plan = await service.CreateAsync(request, ct);
            return Results.Created($"/api/plans/{plan.Id}", plan);
        }).RequireAuthorization(Policies.Admin);

        plans.MapPut("/{id:int}", (int id, SavePlanRequest request, PlanService service, CancellationToken ct) => service.UpdateAsync(id, request, ct))
            .RequireAuthorization(Policies.Admin);
    }
}
