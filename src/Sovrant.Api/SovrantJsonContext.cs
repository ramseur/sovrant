using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Api.OpenAi;
using Sovrant.Api.Types;

namespace Sovrant.Api;

/// <summary>AOT-safe source-generated JSON serialization context for all Sovrant API types.</summary>
[JsonSerializable(typeof(MessagesRequest))]
[JsonSerializable(typeof(MessageResponse))]
[JsonSerializable(typeof(StreamEvent), TypeInfoPropertyName = "StreamEventInfo")]
[JsonSerializable(typeof(StreamEvent.MessageStart))]
[JsonSerializable(typeof(StreamEvent.MessageDelta))]
[JsonSerializable(typeof(StreamEvent.ContentBlockStart))]
[JsonSerializable(typeof(StreamEvent.ContentBlockDelta))]
[JsonSerializable(typeof(StreamEvent.ContentBlockStop))]
[JsonSerializable(typeof(StreamEvent.MessageStop))]
[JsonSerializable(typeof(InputMessage))]
[JsonSerializable(typeof(InputContentBlock))]
[JsonSerializable(typeof(InputContentBlock.TextBlock), TypeInfoPropertyName = "InputTextBlock")]
[JsonSerializable(typeof(InputContentBlock.ToolUseBlock), TypeInfoPropertyName = "InputToolUseBlock")]
[JsonSerializable(typeof(InputContentBlock.ToolResultBlock), TypeInfoPropertyName = "InputToolResultBlock")]
[JsonSerializable(typeof(OutputContentBlock))]
[JsonSerializable(typeof(OutputContentBlock.TextBlock), TypeInfoPropertyName = "OutputTextBlock")]
[JsonSerializable(typeof(OutputContentBlock.ToolUseBlock), TypeInfoPropertyName = "OutputToolUseBlock")]
[JsonSerializable(typeof(OutputContentBlock.ThinkingBlock), TypeInfoPropertyName = "OutputThinkingBlock")]
[JsonSerializable(typeof(OutputContentBlock.RedactedThinkingBlock), TypeInfoPropertyName = "OutputRedactedThinkingBlock")]
[JsonSerializable(typeof(ContentBlockDelta), TypeInfoPropertyName = "ContentBlockDeltaInfo")]
[JsonSerializable(typeof(ContentBlockDelta.TextDelta), TypeInfoPropertyName = "ContentBlockTextDelta")]
[JsonSerializable(typeof(ContentBlockDelta.InputJsonDelta), TypeInfoPropertyName = "ContentBlockInputJsonDelta")]
[JsonSerializable(typeof(ContentBlockDelta.ThinkingDelta), TypeInfoPropertyName = "ContentBlockThinkingDelta")]
[JsonSerializable(typeof(ContentBlockDelta.SignatureDelta), TypeInfoPropertyName = "ContentBlockSignatureDelta")]
[JsonSerializable(typeof(ToolDefinition))]
[JsonSerializable(typeof(ToolChoice))]
[JsonSerializable(typeof(ToolChoice.Auto), TypeInfoPropertyName = "ToolChoiceAuto")]
[JsonSerializable(typeof(ToolChoice.Any), TypeInfoPropertyName = "ToolChoiceAny")]
[JsonSerializable(typeof(ToolChoice.Tool), TypeInfoPropertyName = "ToolChoiceTool")]
[JsonSerializable(typeof(ToolResultContentBlock))]
[JsonSerializable(typeof(ToolResultContentBlock.TextBlock), TypeInfoPropertyName = "ToolResultTextBlock")]
[JsonSerializable(typeof(ToolResultContentBlock.ImageBlock), TypeInfoPropertyName = "ToolResultImageBlock")]
[JsonSerializable(typeof(Usage))]
[JsonSerializable(typeof(MessageDelta), TypeInfoPropertyName = "MessageDeltaInfo")]
[JsonSerializable(typeof(OpenAiChatRequest))]
[JsonSerializable(typeof(OpenAiChatResponse))]
[JsonSerializable(typeof(OpenAiSseChunk))]
[JsonSerializable(typeof(JsonElement))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
internal sealed partial class SovrantJsonContext : JsonSerializerContext { }
