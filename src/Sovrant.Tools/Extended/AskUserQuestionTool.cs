using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Extended;

/// <summary>Elicits a response from the user by presenting a question mid-turn.</summary>
public sealed class AskUserQuestionTool : ITool
{
    private static readonly ToolDefinition s_definition = new("AskUserQuestion", CreateSchema())
    {
        Description =
            "Asks the user a question and returns their answer. " +
            "Use this when you need clarification or additional input from the user to continue.",
    };

    private readonly IUserInputProvider _inputProvider;

    public AskUserQuestionTool(IUserInputProvider inputProvider) => _inputProvider = inputProvider;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var question = input.GetStringProp("question");
        if (string.IsNullOrWhiteSpace(question))
            return "Error: question is required.";

        return await _inputProvider.AskAsync(question, ct).ConfigureAwait(false);
    }


    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "question": {"type": "string", "description": "The question to ask the user."}
            },
            "required": ["question"]
        }
        """).RootElement;
}
