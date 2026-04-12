using System.Text;
using System.Text.Json;
using Sovrant.Server.OpenAi;

namespace Sovrant.Server.Streaming;

/// <summary>Writes OpenAI-compatible Server-Sent Events to an HTTP response stream.</summary>
internal static class SseWriter
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Writes SSE headers so the client starts receiving events immediately.</summary>
    public static void SetSseHeaders(HttpResponse response)
    {
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no"; // disable nginx buffering
    }

    /// <summary>Serialises <paramref name="chunk"/> and writes one SSE <c>data:</c> line.</summary>
    public static async Task WriteChunkAsync(
        HttpResponse response,
        ChatCompletionChunk chunk,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(chunk, s_options);
        var line = $"data: {json}\n\n";
        await response.WriteAsync(line, Encoding.UTF8, ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Writes the terminal <c>data: [DONE]</c> SSE line.</summary>
    public static async Task WriteDoneAsync(HttpResponse response, CancellationToken ct)
    {
        await response.WriteAsync("data: [DONE]\n\n", Encoding.UTF8, ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Builds a text-delta chunk.</summary>
    public static ChatCompletionChunk TextChunk(string id, string model, string text) =>
        new()
        {
            Id = id,
            Model = model,
            Choices = [new ChunkChoice { Delta = new DeltaContent { Content = text } }],
        };

    /// <summary>Builds a tool-use-started extension chunk.</summary>
    public static ChatCompletionChunk ToolUseChunk(string id, string model, string toolName, string toolUseId) =>
        new()
        {
            Id = id,
            Model = model,
            Choices = [new ChunkChoice { Delta = new DeltaContent { Content = string.Empty } }],
            Sovrant = new SovrantEvent { Event = "tool_use", ToolName = toolName, ToolUseId = toolUseId },
        };

    /// <summary>Builds a tool-result extension chunk.</summary>
    public static ChatCompletionChunk ToolResultChunk(
        string id, string model, string toolName, string toolUseId, bool isError) =>
        new()
        {
            Id = id,
            Model = model,
            Choices = [new ChunkChoice { Delta = new DeltaContent { Content = string.Empty } }],
            Sovrant = new SovrantEvent
            {
                Event = "tool_result",
                ToolName = toolName,
                ToolUseId = toolUseId,
                IsError = isError,
            },
        };

    /// <summary>Builds a clarification-needed extension chunk.</summary>
    public static ChatCompletionChunk ClarificationChunk(string id, string model, string question) =>
        new()
        {
            Id = id,
            Model = model,
            Choices = [new ChunkChoice { Delta = new DeltaContent { Content = string.Empty } }],
            Sovrant = new SovrantEvent { Event = "clarification_needed", Clarification = question },
        };

    /// <summary>Builds a plan-presented extension chunk.</summary>
    public static ChatCompletionChunk PlanPresentedChunk(
        string id, string model, string planId, string formattedPlan, bool requiresApproval) =>
        new()
        {
            Id = id,
            Model = model,
            Choices = [new ChunkChoice { Delta = new DeltaContent { Content = string.Empty } }],
            Sovrant = new SovrantEvent
            {
                Event = "plan_presented",
                PlanId = planId,
                FormattedPlan = formattedPlan,
                RequiresApproval = requiresApproval,
            },
        };

    /// <summary>Builds a step-progress extension chunk.</summary>
    public static ChatCompletionChunk StepProgressChunk(
        string id, string model, int current, int total, string intent, string status) =>
        new()
        {
            Id = id,
            Model = model,
            Choices = [new ChunkChoice { Delta = new DeltaContent { Content = string.Empty } }],
            Sovrant = new SovrantEvent
            {
                Event = "step_progress",
                StepCurrent = current,
                StepTotal = total,
                StepIntent = intent,
                StepStatus = status,
            },
        };

    /// <summary>Builds the final stop chunk with token usage.</summary>
    public static ChatCompletionChunk StopChunk(
        string id, string model, int inputTokens, int outputTokens) =>
        new()
        {
            Id = id,
            Model = model,
            Choices = [new ChunkChoice { Delta = new DeltaContent(), FinishReason = "stop" }],
            Usage = new UsageInfo
            {
                PromptTokens = inputTokens,
                CompletionTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
            },
        };
}
