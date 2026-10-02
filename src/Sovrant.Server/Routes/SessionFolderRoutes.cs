using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Runtime.Session;
using Sovrant.Server.Auth;
using Sovrant.Server.ServerConfig;

namespace Sovrant.Server.Routes;

/// <summary>
/// Phase 133 — conversation folder endpoints. Folders are personal: every call
/// acts on the caller's own tree, admins included, and another user's folder or
/// conversation is indistinguishable from one that doesn't exist (404).
/// </summary>
internal static class SessionFolderRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/session-folders", ListFolders);
        app.MapPost("/v1/session-folders", CreateFolder);
        app.MapPatch("/v1/session-folders/{id}", UpdateFolder);
        app.MapDelete("/v1/session-folders/{id}", DeleteFolder);
        app.MapPut("/v1/sessions/{id}/folder", MoveSession);
    }

    private static async Task<IResult> ListFolders(HttpContext ctx, ISessionFolderStore store, CancellationToken ct)
    {
        if (ctx.GetUserId() is not { } me)
            return Results.Unauthorized();
        var folders = await store.ListAsync(me, ct).ConfigureAwait(false);
        return Results.Ok(new { folders = folders.Select(SessionFolderDto.From).ToList() });
    }

    private static async Task<IResult> CreateFolder(
        HttpContext ctx, CreateSessionFolderRequest req, ISessionFolderStore store, CancellationToken ct)
    {
        if (ctx.GetUserId() is not { } me)
            return Results.Unauthorized();
        try
        {
            var folder = await store.CreateAsync(me, req.Name ?? string.Empty, req.ParentFolderId, ct).ConfigureAwait(false);
            return Results.Json(SessionFolderDto.From(folder), statusCode: StatusCodes.Status201Created);
        }
        catch (SessionFolderException ex)
        {
            return Refused(ex);
        }
    }

    /// <summary>
    /// Renames and/or moves a folder. <c>parent_folder_id</c> is only a move when the
    /// property is present: <c>null</c> moves to the top level, absent leaves it put.
    /// </summary>
    private static async Task<IResult> UpdateFolder(
        string id, HttpContext ctx, ISessionFolderStore store, CancellationToken ct)
    {
        if (ctx.GetUserId() is not { } me)
            return Results.Unauthorized();

        JsonDocument body;
        try
        {
            body = await JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Body must be a JSON object." });
        }

        using (body)
        {
            var root = body.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Results.BadRequest(new { error = "Body must be a JSON object." });

            var hasName = root.TryGetProperty("name", out var nameProp);
            var hasParent = root.TryGetProperty("parent_folder_id", out var parentProp);
            if (!hasName && !hasParent)
                return Results.BadRequest(new { error = "Provide 'name' and/or 'parent_folder_id'." });
            if (hasName && nameProp.ValueKind != JsonValueKind.String)
                return Results.BadRequest(new { error = "'name' must be a string." });
            if (hasParent && parentProp.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                return Results.BadRequest(new { error = "'parent_folder_id' must be a string or null." });

            try
            {
                SessionFolder? folder = null;
                if (hasParent)
                    folder = await store.MoveAsync(me, id, parentProp.ValueKind == JsonValueKind.Null ? null : parentProp.GetString(), ct).ConfigureAwait(false);
                if (hasName)
                    folder = await store.RenameAsync(me, id, nameProp.GetString()!, ct).ConfigureAwait(false);
                return Results.Ok(SessionFolderDto.From(folder!));
            }
            catch (SessionFolderException ex)
            {
                return Refused(ex);
            }
        }
    }

    private static async Task<IResult> DeleteFolder(
        string id, HttpContext ctx, ISessionFolderStore store, CancellationToken ct)
    {
        if (ctx.GetUserId() is not { } me)
            return Results.Unauthorized();
        return await store.DeleteAsync(me, id, ct).ConfigureAwait(false)
            ? Results.NoContent()
            : Results.NotFound(new { error = SessionFolderRules.Describe(SessionFolderError.NotFound) });
    }

    private static async Task<IResult> MoveSession(
        string id, HttpContext ctx, MoveSessionToFolderRequest req, ISessionFolderStore store, CancellationToken ct)
    {
        if (ctx.GetUserId() is not { } me)
            return Results.Unauthorized();
        if (!InputValidation.IsValidSessionId(id))
            return Results.BadRequest(new { error = "Invalid session ID format." });
        try
        {
            return await store.MoveSessionAsync(me, id, req.FolderId, ct).ConfigureAwait(false)
                ? Results.NoContent()
                : Results.NotFound(new { error = $"Session '{id}' not found." });
        }
        catch (SessionFolderException ex)
        {
            return Refused(ex);
        }
    }

    /// <summary>NotFound → 404, a name clash → 409, any other rule refusal → 400. Body carries the code and sentence.</summary>
    private static IResult Refused(SessionFolderException ex)
    {
        var code = ex.Error switch
        {
            SessionFolderError.NotFound => "not_found",
            SessionFolderError.InvalidName => "invalid_name",
            SessionFolderError.DuplicateName => "duplicate_name",
            SessionFolderError.TooDeep => "too_deep",
            SessionFolderError.Cycle => "cycle",
            _ => "refused",
        };
        var status = ex.Error switch
        {
            SessionFolderError.NotFound => StatusCodes.Status404NotFound,
            SessionFolderError.DuplicateName => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Json(new { error = ex.Message, code }, statusCode: status);
    }
}

internal sealed class CreateSessionFolderRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("parent_folder_id")]
    public string? ParentFolderId { get; init; }
}

internal sealed class MoveSessionToFolderRequest
{
    /// <summary>The folder to file the conversation into; <c>null</c> unfiles it.</summary>
    [JsonPropertyName("folder_id")]
    public string? FolderId { get; init; }
}

internal sealed class SessionFolderDto
{
    [JsonPropertyName("folder_id")]
    public string FolderId { get; init; } = string.Empty;

    [JsonPropertyName("parent_folder_id")]
    public string? ParentFolderId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; init; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; init; } = string.Empty;

    public static SessionFolderDto From(SessionFolder f) => new()
    {
        FolderId = f.FolderId,
        ParentFolderId = f.ParentFolderId,
        Name = f.Name,
        SortOrder = f.SortOrder,
        CreatedAt = f.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
        UpdatedAt = f.UpdatedAt.ToString("o", CultureInfo.InvariantCulture),
    };
}
