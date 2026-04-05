using System.Security.Cryptography;

namespace Sovrant.Server.Middleware;

/// <summary>
/// Computes an ETag for cacheable GET responses and returns 304 Not Modified when the
/// client sends a matching <c>If-None-Match</c> header. Only applies to a known set of
/// safe, static-ish routes. Chat, session content, streaming, swarm, and eval routes
/// are explicitly excluded — they must never be cached.
/// </summary>
internal sealed class ETagMiddleware(RequestDelegate next)
{
    /// <summary>Path prefixes that are eligible for ETag caching.</summary>
    private static readonly string[] CacheablePrefixes =
    [
        "/v1/tools",
        "/v1/skills",
        "/v1/agents/templates",
        "/v1/config",
        "/v1/status",
        "/v1/models",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        // Only GET requests on cacheable routes.
        if (!HttpMethods.IsGet(context.Request.Method) || !IsCacheablePath(context.Request.Path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Buffer the response so we can compute the ETag before sending.
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await next(context).ConfigureAwait(false);

        // Only tag successful responses.
        if (context.Response.StatusCode is >= 200 and < 300)
        {
            buffer.Seek(0, SeekOrigin.Begin);
            var hash = SHA256.HashData(buffer.ToArray());
            var etag = $"\"{Convert.ToHexStringLower(hash[..8])}\"";

            context.Response.Headers.ETag = etag;

            var ifNoneMatch = context.Request.Headers.IfNoneMatch.FirstOrDefault();
            if (string.Equals(ifNoneMatch, etag, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                context.Response.ContentLength = 0;
                context.Response.Body = originalBody;
                return;
            }
        }

        // Write the buffered body to the real stream.
        buffer.Seek(0, SeekOrigin.Begin);
        context.Response.Body = originalBody;
        context.Response.ContentLength = buffer.Length;
        await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
    }

    private static bool IsCacheablePath(PathString path)
    {
        var value = path.Value;
        if (value is null) return false;

        foreach (var prefix in CacheablePrefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
