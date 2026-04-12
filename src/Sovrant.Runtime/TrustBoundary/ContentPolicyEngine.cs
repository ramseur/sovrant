using System.Text.RegularExpressions;

namespace Sovrant.Runtime.TrustBoundary;

/// <summary>Strictness level for the ethical harness.</summary>
public enum EthicalStrictness
{
    /// <summary>Block clearly harmful categories only.</summary>
    Standard,
    /// <summary>Standard + block borderline content, require justification for dual-use.</summary>
    Strict,
    /// <summary>Strict + custom blocked keyword lists, mandatory audit logging.</summary>
    Enterprise,
}

/// <summary>Configuration for the ethical harness.</summary>
public sealed class EthicalPolicy
{
    /// <summary>Whether the ethical harness is enabled.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Strictness level.</summary>
    public EthicalStrictness Strictness { get; init; } = EthicalStrictness.Standard;

    /// <summary>Whether to scan model responses before surfacing to the user.</summary>
    public bool ResponseScanning { get; init; } = true;

    /// <summary>Whether to log all blocks and flags for compliance.</summary>
    public bool AuditLog { get; init; } = true;

    /// <summary>Additional blocked keyword patterns (enterprise use).</summary>
    public IReadOnlyList<string> CustomBlockedPatterns { get; init; } = [];
}

/// <summary>
/// Rule-based content policy engine. Detects harmful content categories using
/// keyword and pattern matching. This is not an LLM — it cannot understand nuance
/// or context — but it catches the clearly harmful patterns that no model should
/// be asked to produce, regardless of its own safety training.
/// </summary>
public sealed partial class ContentPolicyEngine : IEthicalHarness
{
    private readonly EthicalPolicy _policy;
    private readonly List<Regex> _customBlockedRegexes;
    private readonly EthicalAuditLog? _auditLog;

    // ── Harmful content categories ─────────────────────────────────────
    // Each category has a set of indicator patterns. A single match → block.
    // These patterns are intentionally broad phrases, not single words,
    // to reduce false positives while catching clear harmful intent.

    private static readonly (string Category, string[] Patterns)[] s_standardCategories =
    [
        ("weapons_synthesis", [
            "how to make a bomb", "how to build a bomb", "synthesize explosives",
            "make a pipe bomb", "build an ied", "manufacture a weapon",
            "create a biological weapon", "make poison gas", "synthesize nerve agent",
            "build a detonator", "assemble a firearm from scratch",
        ]),
        ("csam", [
            "child sexual", "sexual content involving minors", "underage sexual",
            "child pornography", "generate images of children in sexual",
        ]),
        ("doxxing", [
            "find someone's home address", "doxx this person", "expose their personal information",
            "publish their private address", "reveal their identity and home",
        ]),
        ("fraud", [
            "write a phishing email", "create a scam message", "impersonate a bank",
            "draft a fraud letter", "fake invoice to steal", "social engineer their password",
        ]),
        ("malware", [
            "write a keylogger", "create ransomware", "build a trojan",
            "write a virus that spreads", "create malware that evades",
            "build a botnet", "write a rootkit",
        ]),
        ("self_harm", [
            "how to kill yourself", "methods of suicide", "best way to end your life",
            "instructions for self-harm", "painless ways to die",
        ]),
    ];

    // Strict mode adds borderline categories that have legitimate uses but
    // require clear professional context.
    private static readonly (string Category, string[] Patterns)[] s_strictCategories =
    [
        ("harassment", [
            "write a threatening message to", "intimidate this person",
            "send death threats to", "bully them into",
        ]),
        ("deception", [
            "write a fake news article", "create disinformation about",
            "generate propaganda to manipulate", "deepfake script for",
        ]),
    ];

    public ContentPolicyEngine(EthicalPolicy? policy = null, EthicalAuditLog? auditLog = null)
    {
        _policy = policy ?? new EthicalPolicy();
        _auditLog = auditLog;
        _customBlockedRegexes = [];
        foreach (var pattern in _policy.CustomBlockedPatterns)
        {
            if (!string.IsNullOrWhiteSpace(pattern))
                _customBlockedRegexes.Add(new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase));
        }
    }

    public EthicalVerdict EvaluateOutbound(string text) => Evaluate(text, isOutbound: true);

    public EthicalVerdict EvaluateInbound(string text) =>
        _policy.ResponseScanning ? Evaluate(text, isOutbound: false) : new EthicalVerdict.Allow();

    private EthicalVerdict Evaluate(string text, bool isOutbound)
    {
        if (!_policy.Enabled || string.IsNullOrWhiteSpace(text))
            return new EthicalVerdict.Allow();

        // Check standard categories (always active).
        foreach (var (category, patterns) in s_standardCategories)
        {
            foreach (var pattern in patterns)
            {
                if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    var reason = $"Content matches harmful category: {category}";
                    _auditLog?.Record(category, reason, EthicalSeverity.Critical, isOutbound);
                    return new EthicalVerdict.Block(reason, category);
                }
            }
        }

        // Check strict categories (Strict and Enterprise modes).
        if (_policy.Strictness >= EthicalStrictness.Strict)
        {
            foreach (var (category, patterns) in s_strictCategories)
            {
                foreach (var pattern in patterns)
                {
                    if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        var reason = $"Content matches restricted category: {category}";
                        _auditLog?.Record(category, reason, EthicalSeverity.Critical, isOutbound);
                        return new EthicalVerdict.Block(reason, category);
                    }
                }
            }
        }

        // Check custom blocked patterns (Enterprise mode).
        if (_policy.Strictness >= EthicalStrictness.Enterprise)
        {
            foreach (var regex in _customBlockedRegexes)
            {
                if (regex.IsMatch(text))
                {
                    var reason = $"Content matches custom blocked pattern: {regex}";
                    _auditLog?.Record("custom_blocked", reason, EthicalSeverity.Critical, isOutbound);
                    return new EthicalVerdict.Block(reason, "custom_blocked");
                }
            }
        }

        return new EthicalVerdict.Allow();
    }
}
