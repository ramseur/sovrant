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
        app.MapPost("/v1/auth/change-password", ChangePasswordAsync);
        app.MapPost("/v1/auth/use-reset-token", UseResetTokenAsync);
        app.MapPost("/v1/auth/registration/open", OpenRegistrationAsync);
        app.MapPost("/v1/auth/registration/close", CloseRegistrationAsync);
        app.MapGet("/v1/auth/registration/status", GetRegistrationStatusAsync);
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

        return result.Success
            ? Results.Ok(new { token = result.Token, user_id = result.UserId })
            : Results.Json(new { error = result.Error }, statusCode: 403);
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
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

internal sealed record RegisterRequest(string Email, string Password);
internal sealed record LoginRequest(string Email, string Password);
internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
internal sealed record UseResetTokenRequest(string Token, string NewPassword);
