using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.TrustBoundary;

/// <summary>
/// Default <see cref="IPromptSanitizer"/> implementation.
/// Runs all registered <see cref="IPatternDetector"/>s over every text field in a
/// <see cref="MessagesRequest"/> and replaces matches with deterministic placeholders.
/// </summary>
public sealed class PromptSanitizer : IPromptSanitizer, IDisposable
{
    private readonly IDisposable? _changedSubscription;
    private IReadOnlyList<IPatternDetector> _detectors;

    public PromptSanitizer(IEnumerable<IPatternDetector> detectors)
    {
        _detectors = detectors.ToList();
    }

    /// <summary>
    /// Hot-reloadable ctor — derives the detector set from the live trust-boundary
    /// config and atomically swaps it on every <see cref="ILiveSettings{T}.OnChanged"/>
    /// notification. In-flight <see cref="Sanitize"/> calls keep the snapshot they
    /// captured when they started.
    /// </summary>
    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public PromptSanitizer(ILiveSettings<TrustBoundaryConfig> liveConfig)
    {
        ArgumentNullException.ThrowIfNull(liveConfig);
        _detectors = BuildDetectors(liveConfig.Current);
        _changedSubscription = liveConfig.OnChanged(fresh =>
            Volatile.Write(ref _detectors, BuildDetectors(fresh)));
    }

    private static IPatternDetector[] BuildDetectors(TrustBoundaryConfig config)
    {
        var corp = new CorporateDataConfig
        {
            CorporateDomains = config.Sanitizer.CorporateDomains,
            AllowList = config.Sanitizer.AllowList,
        };
        return
        [
            new PiiDetector(),
            new CorporateDataDetector(corp),
            new CustomPatternRegistry(config.Sanitizer.CustomPatterns),
        ];
    }

    /// <inheritdoc/>
    public void Dispose() => _changedSubscription?.Dispose();

    public SanitizationResult Sanitize(MessagesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var map = new RedactionMap();

        // Sanitize system prompt
        var system = request.System is not null ? SanitizeText(request.System, map) : null;

        // Sanitize messages
        var messages = new List<InputMessage>(request.Messages.Count);
        foreach (var msg in request.Messages)
        {
            messages.Add(SanitizeMessage(msg, map));
        }

        var sanitized = new MessagesRequest(request.Model, request.MaxTokens, messages)
        {
            System = system,
            Tools = request.Tools,
            ToolChoice = request.ToolChoice,
            Stream = request.Stream,
        };

        return new SanitizationResult(sanitized, map);
    }

    public string Restore(string response, RedactionMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return map.Restore(response ?? string.Empty);
    }

    private InputMessage SanitizeMessage(InputMessage msg, RedactionMap map)
    {
        var sanitizedBlocks = new List<InputContentBlock>(msg.Content.Count);
        foreach (var block in msg.Content)
        {
            sanitizedBlocks.Add(block switch
            {
                InputContentBlock.TextBlock tb =>
                    new InputContentBlock.TextBlock(SanitizeText(tb.Text, map)),
                InputContentBlock.ToolResultBlock tr =>
                    SanitizeToolResult(tr, map),
                // ToolUseBlock — contains tool name and JSON input; tool names are not sensitive,
                // but JSON input may contain user data that was passed as arguments.
                InputContentBlock.ToolUseBlock tu =>
                    new InputContentBlock.ToolUseBlock(tu.Id, tu.Name, SanitizeJsonElement(tu.Input, map)),
                _ => block,
            });
        }
        return new InputMessage(msg.Role, sanitizedBlocks);
    }

    private InputContentBlock.ToolResultBlock SanitizeToolResult(
        InputContentBlock.ToolResultBlock tr, RedactionMap map)
    {
        var sanitizedContent = new List<ToolResultContentBlock>(tr.Content.Count);
        foreach (var block in tr.Content)
        {
            sanitizedContent.Add(block switch
            {
                ToolResultContentBlock.TextBlock tb =>
                    new ToolResultContentBlock.TextBlock(SanitizeText(tb.Text, map)),
                // ImageBlock and other non-text blocks pass through unchanged.
                _ => block,
            });
        }
        return new InputContentBlock.ToolResultBlock(tr.ToolUseId, sanitizedContent, tr.IsError);
    }

    private JsonElement SanitizeJsonElement(JsonElement element, RedactionMap map)
    {
        // Serialize to string, sanitize, parse back.
        var json = element.GetRawText();
        var sanitized = SanitizeText(json, map);
        if (ReferenceEquals(json, sanitized)) return element;
        using var doc = JsonDocument.Parse(sanitized);
        return doc.RootElement.Clone();
    }

    private string SanitizeText(string text, RedactionMap map)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Snapshot the detector list once per call so a hot-reload mid-sanitize
        // doesn't tear (e.g. some detectors from old config, some from new).
        var detectors = Volatile.Read(ref _detectors);

        // Collect all detections from all detectors.
        var allDetections = new List<Detection>();
        foreach (var detector in detectors)
        {
            allDetections.AddRange(detector.Detect(text));
        }

        if (allDetections.Count == 0) return text;

        // Sort by position descending so replacements don't shift indices.
        allDetections.Sort((a, b) => b.Start.CompareTo(a.Start));

        // Remove overlapping detections (keep the one that starts first / is longer).
        var filtered = RemoveOverlaps(allDetections);

        // Apply replacements right-to-left.
        var sb = new System.Text.StringBuilder(text);
        foreach (var detection in filtered)
        {
            var placeholder = map.GetOrCreatePlaceholder(detection.OriginalValue, detection.Category);
            sb.Remove(detection.Start, detection.Length);
            sb.Insert(detection.Start, placeholder);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Given detections sorted by Start descending, removes overlaps.
    /// When two detections overlap, the one starting earlier (or longer if same start) wins.
    /// </summary>
    private static List<Detection> RemoveOverlaps(List<Detection> detections)
    {
        if (detections.Count <= 1) return detections;

        // Re-sort ascending by Start, then descending by Length for tie-breaking.
        detections.Sort((a, b) =>
        {
            var cmp = a.Start.CompareTo(b.Start);
            return cmp != 0 ? cmp : b.Length.CompareTo(a.Length);
        });

        var result = new List<Detection> { detections[0] };
        for (var i = 1; i < detections.Count; i++)
        {
            var prev = result[^1];
            var curr = detections[i];
            // If current starts within the previous detection's span, skip it.
            if (curr.Start < prev.Start + prev.Length) continue;
            result.Add(curr);
        }

        // Sort descending for right-to-left replacement.
        result.Sort((a, b) => b.Start.CompareTo(a.Start));
        return result;
    }
}
