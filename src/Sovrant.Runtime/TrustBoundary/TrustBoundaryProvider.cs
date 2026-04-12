using System.Runtime.CompilerServices;
using Sovrant.Api;
using Sovrant.Api.Providers;
using Sovrant.Api.Types;

namespace Sovrant.Runtime.TrustBoundary;

/// <summary>
/// <see cref="ILlmProvider"/> decorator that runs the three-stage trust pipeline:
/// intent verification → ethical harness → data sanitization on outbound,
/// and sanitizer restoration → ethical response scanning on inbound.
/// </summary>
public sealed class TrustBoundaryProvider : ILlmProvider
{
    private readonly ILlmProvider _inner;
    private readonly IPromptSanitizer? _sanitizer;
    private readonly IEthicalHarness? _ethicalHarness;
    private readonly bool _sanitizerEnabled;

    public string Name => _inner.Name;
    public Uri BaseUrl => _inner.BaseUrl;

    public TrustBoundaryProvider(
        ILlmProvider inner,
        IPromptSanitizer? sanitizer = null,
        IEthicalHarness? ethicalHarness = null,
        bool sanitizerEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _sanitizer = sanitizer;
        _ethicalHarness = ethicalHarness;
        _sanitizerEnabled = sanitizerEnabled && sanitizer is not null;
    }

    public async Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default)
    {
        // Outbound: sanitize the request.
        RedactionMap? map = null;
        if (_sanitizerEnabled)
        {
            var sanitized = _sanitizer!.Sanitize(req);
            req = sanitized.Request;
            map = sanitized.Map;
        }

        var result = await _inner.SendAsync(req, ct).ConfigureAwait(false);

        if (!result.IsSuccess || result.Value is null)
            return result;

        var response = result.Value;

        // Inbound: restore sanitized placeholders in response text blocks.
        if (map is not null && !map.IsEmpty)
        {
            response = RestoreResponse(response, map);
        }

        // Inbound: ethical scan of the response.
        if (_ethicalHarness is not null)
        {
            var responseText = ExtractText(response);
            var verdict = _ethicalHarness.EvaluateInbound(responseText);
            if (verdict is EthicalVerdict.Block block)
            {
                // Replace the response content with the block message.
                var blockedContent = new OutputContentBlock.TextBlock(
                    $"[Content blocked by Sovrant Trust Boundary: {block.Reason}]");
                response = response with
                {
                    Content = [blockedContent],
                    StopReason = "trust_boundary_block",
                };
            }
        }

        return Result<MessageResponse>.Ok(response);
    }

    public async IAsyncEnumerable<StreamEvent> StreamAsync(
        MessagesRequest req, [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Outbound: sanitize the request.
        RedactionMap? map = null;
        if (_sanitizerEnabled)
        {
            var sanitized = _sanitizer!.Sanitize(req);
            req = sanitized.Request;
            map = sanitized.Map;
        }

        await foreach (var ev in _inner.StreamAsync(req, ct).ConfigureAwait(false))
        {
            yield return RestoreStreamEvent(ev, map);
        }
    }

    private static StreamEvent RestoreStreamEvent(StreamEvent ev, RedactionMap? map)
    {
        if (map is null || map.IsEmpty) return ev;

        return ev switch
        {
            // Text deltas carry the streamed response text — restore placeholders.
            StreamEvent.ContentBlockDelta { Index: var idx, Delta: ContentBlockDelta.TextDelta td } =>
                new StreamEvent.ContentBlockDelta(idx, new ContentBlockDelta.TextDelta(map.Restore(td.Text))),

            // All other events pass through unchanged.
            _ => ev,
        };
    }

    private static MessageResponse RestoreResponse(MessageResponse response, RedactionMap map)
    {
        var restoredBlocks = new List<OutputContentBlock>(response.Content.Count);
        foreach (var block in response.Content)
        {
            restoredBlocks.Add(block switch
            {
                OutputContentBlock.TextBlock tb =>
                    new OutputContentBlock.TextBlock(map.Restore(tb.Text)),
                _ => block,
            });
        }
        return response with { Content = restoredBlocks };
    }

    private static string ExtractText(MessageResponse response)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var block in response.Content)
        {
            if (block is OutputContentBlock.TextBlock tb)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(tb.Text);
            }
        }
        return sb.ToString();
    }
}
