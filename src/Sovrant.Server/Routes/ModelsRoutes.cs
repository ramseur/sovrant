using System.Text.Json.Serialization;
using Sovrant.Api.Routing;

namespace Sovrant.Server.Routes;

/// <summary>Registers <c>GET /v1/models</c> — OpenAI-compatible model list.</summary>
internal static class ModelsRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/models", async (ISmartRouter router, CancellationToken ct) =>
        {
            await router.InitializeAsync(ct).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var models = router.GetStatus()
                .Select(s => new ModelObject
                {
                    Id = s.Name,
                    Created = now,
                    OwnedBy = "sovrant",
                })
                .ToList();

            return Results.Ok(new ModelList { Data = models });
        });
    }
}

internal sealed class ModelList
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = "list";

    [JsonPropertyName("data")]
    public IReadOnlyList<ModelObject> Data { get; init; } = [];
}

internal sealed class ModelObject
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; init; } = "model";

    [JsonPropertyName("created")]
    public long Created { get; init; }

    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; init; } = string.Empty;
}
