using System.Text.Json;

namespace Sovrant.Tools;

/// <summary>
/// Shared parameter extraction helpers for <see cref="JsonElement"/> tool inputs.
/// Replaces the per-tool <c>GetString</c>/<c>GetInt</c>/<c>GetBool</c> boilerplate.
/// </summary>
internal static class JsonElementExtensions
{
    public static string GetStringProp(this JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    public static int GetIntProp(this JsonElement el, string prop, int def = 0) =>
        el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var n) ? n : def;

    public static bool GetBoolProp(this JsonElement el, string prop, bool def = false) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : def;

    public static double GetDoubleProp(this JsonElement el, string prop, double def = 0.0) =>
        el.TryGetProperty(prop, out var v) && v.TryGetDouble(out var d) ? d : def;

    public static string[] GetStringArrayProp(this JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array
            ? [.. v.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)]
            : [];
}
