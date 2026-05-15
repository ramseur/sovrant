using Sovrant.Runtime.Auth;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Routes;

/// <summary>
/// Phase 85 — Authentication endpoints.
/// Login and register are unauthenticated; all others require a valid svt_ token.
/// </summary>
internal static class AuthRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/auth/register", RegisterAsync);
        app.MapPost("/v1/auth/login", LoginAsync);
        app.MapPost("/v1/auth/logout", LogoutAsync);
        app.MapGet("/v1/auth/me", MeAsync);
        app.MapPost("/v1/auth/change-password", ChangePasswordAsync);
        app.MapPost("/v1/auth/use-reset-token", UseResetTokenAsync);
        app.MapPost("/v1/auth/registration/open", OpenRegistrationAsync);
        app.MapPost("/v1/auth/registration/close", CloseRegistrationAsync);
        app.MapGet("/v1/auth/registration/status", GetRegistrationStatusAsync);
        // Admin: approval gate control
        app.MapGet("/v1/auth/approval/status", GetApprovalStatusAsync);
        app.MapPost("/v1/auth/approval/enable", EnableApprovalAsync);
        app.MapPost("/v1/auth/approval/disable", DisableApprovalAsync);
    }

    private static IResult MeAsync(HttpContext ctx)
    {
        var userId = ctx.GetUserId();
        if (userId is null) return Results.Unauthorized();
        var role = ctx.GetRole() ?? "user";
        return Results.Ok(new { user_id = userId, role });
    }

    private static async Task<IResult> RegisterAsync(
        HttpContext ctx,
        IIdentityService identity,
        RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Results.BadRequest(new { error = "Email and password are required." });

        var result = await identity.RegisterAsync(req.Email, req.Password, ctx.RequestAborted)
            .ConfigureAwait(false);

        if (!result.Success)
            return Results.Json(new { error = result.Error }, statusCode: 403);

        if (result.IsPendingApproval)
            return Results.Accepted(value: new
            {
                pending_approval = true,
                user_id = result.UserId,
                role = result.Role,
                message = "Your account has been created and is awaiting admin approval."
            });

        return Results.Ok(new { token = result.Token, user_id = result.UserId, role = result.Role });
    }

    private static async Task<IResult> LoginAsync(
        HttpContext ctx,
        IIdentityService identity,
        LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Results.BadRequest(new { error = "Email and password are required." });

        var result = await identity.LoginAsync(req.Email, req.Password, ctx.RequestAborted)
            .ConfigureAwait(false);

        return result.Success
            ? Results.Ok(new { token = result.Token, user_id = result.UserId, role = result.Role })
            : Results.Json(new { error = result.Error }, statusCode: 401);
    }

    private static async Task<IResult> LogoutAsync(HttpContext ctx, IIdentityService identity)
    {
        var tokenId = ctx.GetTokenId();
        if (tokenId is null)
            return Results.Unauthorized();

        await identity.LogoutAsync(tokenId, ctx.RequestAborted).ConfigureAwait(false);
        return Results.Ok(new { message = "Logged out." });
    }

    private static async Task<IResult> ChangePasswordAsync(
        HttpContext ctx,
        IIdentityService identity,
        ChangePasswordRequest req)
    {
        var userId = ctx.GetUserId();
        if (userId is null)
            return Results.Unauthorized();

        try
        {
            await identity.ChangePasswordAsync(userId, req.CurrentPassword, req.NewPassword, ctx.RequestAborted)
                .ConfigureAwait(false);
            return Results.Ok(new { message = "Password changed." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 401);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UseResetTokenAsync(
        HttpContext ctx,
        IIdentityService identity,
        UseResetTokenRequest req)
    {
        var result = await identity.UseResetTokenAsync(req.Token, req.NewPassword, ctx.RequestAborted)
            .ConfigureAwait(false);

        return result.Success
            ? Results.Ok(new { message = "Password reset successfully." })
            : Results.BadRequest(new { error = result.Error });
    }

    private static async Task<IResult> OpenRegistrationAsync(HttpContext ctx, IIdentityService identity)
    {
        if (!ctx.IsAdmin())
            return Results.Forbid();

        await identity.SetRegistrationOpenAsync(true, ctx.RequestAborted).ConfigureAwait(false);
        return Results.Ok(new { registration_open = true });
    }

    private static async Task<IResult> CloseRegistrationAsync(HttpContext ctx, IIdentityService identity)
    {
        if (!ctx.IsAdmin())
            return Results.Forbid();

        await identity.SetRegistrationOpenAsync(false, ctx.RequestAborted).ConfigureAwait(false);
        return Results.Ok(new { registration_open = false });
    }

    private static async Task<IResult> GetRegistrationStatusAsync(HttpContext ctx, IIdentityService identity)
    {
        var open = await identity.IsRegistrationOpenAsync(ctx.RequestAborted).ConfigureAwait(false);
        return Results.Ok(new { registration_open = open });
    }

    private static async Task<IResult> GetApprovalStatusAsync(HttpContext ctx, IIdentityService identity)
    {
        if (!ctx.IsAdmin()) return Results.Forbid();
        var required = await identity.IsApprovalRequiredAsync(ctx.RequestAborted).ConfigureAwait(false);
        return Results.Ok(new { approval_required = required });
    }

    private static async Task<IResult> EnableApprovalAsync(HttpContext ctx, IIdentityService identity)
    {
        if (!ctx.IsAdmin()) return Results.Forbid();
        await identity.SetApprovalRequiredAsync(true, ctx.RequestAborted).ConfigureAwait(false);
        return Results.Ok(new { approval_required = true });
    }

    private static async Task<IResult> DisableApprovalAsync(HttpContext ctx, IIdentityService identity)
    {
        if (!ctx.IsAdmin()) return Results.Forbid();
        await identity.SetApprovalRequiredAsync(false, ctx.RequestAborted).ConfigureAwait(false);
        return Results.Ok(new { approval_required = false });
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

internal sealed record RegisterRequest(string Email, string Password);
internal sealed record LoginRequest(string Email, string Password);
internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
internal sealed record UseResetTokenRequest(string Token, string NewPassword);
