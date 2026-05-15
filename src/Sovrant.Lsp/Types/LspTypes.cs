using System.Text.Json.Serialization;

namespace Sovrant.Lsp.Types;

/// <summary>A text document identifier (file URI).</summary>
public sealed class TextDocumentIdentifier
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = string.Empty;
}

/// <summary>A position in a text document (0-based line and character).</summary>
public sealed class Position
{
    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("character")]
    public int Character { get; init; }
}

/// <summary>A range in a text document.</summary>
public sealed class Range
{
    [JsonPropertyName("start")]
    public Position Start { get; init; } = new();

    [JsonPropertyName("end")]
    public Position End { get; init; } = new();
}

/// <summary>A location in a document (URI + range).</summary>
public sealed class Location
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = string.Empty;

    [JsonPropertyName("range")]
    public Range Range { get; init; } = new();
}

/// <summary>A text document position parameter (used by hover, definition, references).</summary>
public sealed class TextDocumentPositionParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = new();

    [JsonPropertyName("position")]
    public Position Position { get; init; } = new();
}

/// <summary>Parameters for textDocument/references.</summary>
public sealed class ReferenceParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = new();

    [JsonPropertyName("position")]
    public Position Position { get; init; } = new();

    [JsonPropertyName("context")]
    public ReferenceContext Context { get; init; } = new();
}

/// <summary>Context for reference requests.</summary>
public sealed class ReferenceContext
{
    [JsonPropertyName("includeDeclaration")]
    public bool IncludeDeclaration { get; init; } = true;
}

/// <summary>Parameters for textDocument/rename.</summary>
public sealed class RenameParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = new();

    [JsonPropertyName("position")]
    public Position Position { get; init; } = new();

    [JsonPropertyName("newName")]
    public string NewName { get; init; } = string.Empty;
}

/// <summary>A workspace edit returned by rename.</summary>
public sealed class WorkspaceEdit
{
    [JsonPropertyName("changes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, TextEdit[]>? Changes { get; init; }
}

/// <summary>A text edit (range + new text).</summary>
public sealed class TextEdit
{
    [JsonPropertyName("range")]
    public Range Range { get; init; } = new();

    [JsonPropertyName("newText")]
    public string NewText { get; init; } = string.Empty;
}

/// <summary>Hover result from textDocument/hover.</summary>
public sealed class HoverResult
{
    [JsonPropertyName("contents")]
    public MarkupContent? Contents { get; init; }

    [JsonPropertyName("range")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Range? Range { get; init; }
}

/// <summary>Markup content (markdown or plaintext).</summary>
public sealed class MarkupContent
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "plaintext";

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;
}

/// <summary>A diagnostic (error, warning, etc.).</summary>
public sealed class Diagnostic
{
    [JsonPropertyName("range")]
    public Range Range { get; init; } = new();

    [JsonPropertyName("severity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Severity { get; init; }

    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; init; }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>Parameters published by textDocument/publishDiagnostics notification.</summary>
public sealed class PublishDiagnosticsParams
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = string.Empty;

    [JsonPropertyName("diagnostics")]
    public Diagnostic[] Diagnostics { get; init; } = [];
}

/// <summary>LSP diagnostic severity levels.</summary>
public static class DiagnosticSeverity
{
    public const int Error = 1;
    public const int Warning = 2;
    public const int Information = 3;
    public const int Hint = 4;

    public static string ToLabel(int severity) => severity switch
    {
        Error => "error",
        Warning => "warning",
        Information => "info",
        Hint => "hint",
        _ => "unknown",
    };
}
