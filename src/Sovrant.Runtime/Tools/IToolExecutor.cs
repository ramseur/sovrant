using System.Text.Json;

namespace Sovrant.Runtime.Tools;

/// <summary>Executes tool invocations after permission checks.</summary>
public interface IToolExecutor
{
    /// <summary>Executes the named tool with the given JSON input.</summary>
    /// <param name="toolName">The tool to execute.</param>
    /// <param name="input">The tool's input parameters as a JSON element.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The execution result, which may indicate success or failure.</returns>
    Task<ToolExecutionResult> ExecuteAsync(string toolName, JsonElement input, CancellationToken ct = default);
}

/// <summary>The result of executing a tool.</summary>
/// <param name="Success">Whether the tool executed without error.</param>
/// <param name="Output">The tool's output text.</param>
/// <param name="IsError">Whether the output should be presented as an error to the model.</param>
public sealed record ToolExecutionResult(bool Success, string Output, bool IsError = false);
