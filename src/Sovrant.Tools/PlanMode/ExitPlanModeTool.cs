using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Tools.PlanMode;

/// <summary>
/// Exits Plan mode and returns to the specified permission mode (default: <c>DontAsk</c>).
/// </summary>
public sealed class ExitPlanModeTool : ITool
{
    private static readonly ToolDefinition s_definition = new("ExitPlanMode", CreateSchema())
    {
        Description =
            "Exits Plan mode and restores write access. " +
            "Optionally specify a permission_mode to switch to (default: DontAsk). " +
            "Valid values: Default, AcceptEdits, BypassPermissions, DontAsk.",
    };

    private readonly IPermissionModeAccessor _accessor;

    public ExitPlanModeTool(IPermissionModeAccessor accessor) => _accessor = accessor;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        if (_accessor.Mode != PermissionMode.Plan)
            return Task.FromResult($"Not in Plan mode (current: {_accessor.Mode}). No change made.");

        var modeStr = input.GetStringProp("permission_mode");
        var targetMode = modeStr switch
        {
            "Default"            => PermissionMode.Default,
            "AcceptEdits"        => PermissionMode.AcceptEdits,
            "BypassPermissions"  => PermissionMode.BypassPermissions,
            "DontAsk"            => PermissionMode.DontAsk,
            _                    => PermissionMode.DontAsk,
        };

        _accessor.Mode = targetMode;
        return Task.FromResult($"Exited Plan mode. Permission mode is now: {targetMode}.");
    }


    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "permission_mode": {
                    "type": "string",
                    "description": "Permission mode to restore. Default: DontAsk.",
                    "enum": ["Default", "AcceptEdits", "BypassPermissions", "DontAsk"]
                }
            }
        }
        """).RootElement;
}
