using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Tools.PlanMode;

/// <summary>
/// Switches the session to Plan mode — read-only; all destructive tools are blocked.
/// The previous permission mode is saved so <c>ExitPlanMode</c> can restore it.
/// </summary>
public sealed class EnterPlanModeTool : ITool
{
    private static readonly ToolDefinition s_definition = new("EnterPlanMode", CreateSchema())
    {
        Description =
            "Switches to Plan mode. In this mode only read operations are allowed; " +
            "write, edit, and shell tools are blocked. " +
            "Use ExitPlanMode to return to the previous permission mode.",
    };

    private readonly IPermissionModeAccessor _accessor;

    public EnterPlanModeTool(IPermissionModeAccessor accessor) => _accessor = accessor;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var previous = _accessor.Mode;
        if (previous == PermissionMode.Plan)
            return Task.FromResult("Already in Plan mode.");

        _accessor.Mode = PermissionMode.Plan;
        return Task.FromResult($"Entered Plan mode (was: {previous}). Destructive tools are now blocked.");
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {}
        }
        """).RootElement;
}
