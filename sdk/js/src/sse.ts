import type { ChatCompletionChunk } from "./types.js";

/**
 * Parses a Server-Sent Events stream from a fetch Response into an async
 * iterable of ChatCompletionChunk objects.
 *
 * Handles partial lines across chunk boundaries and stops on `data: [DONE]`.
 */
export async function* parseSSEStream(
  response: Response
): AsyncGenerator<ChatCompletionChunk> {
  const reader = response.body?.getReader();
  if (!reader) throw new Error("Response body is not readable.");

  const decoder = new TextDecoder();
  let buffer = "";

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });

      // SSE lines are separated by \n\n.
      const parts = buffer.split("\n\n");
      // The last part may be incomplete — keep it in the buffer.
      buffer = parts.pop() ?? "";

      for (const part of parts) {
        const line = part.trim();
        if (!line.startsWith("data: ")) continue;

        const data = line.slice("data: ".length);
        if (data === "[DONE]") return;

        try {
          yield JSON.parse(data) as ChatCompletionChunk;
        } catch {
          // Skip malformed chunks.
        }
      }
    }
  } finally {
    reader.releaseLock();
  }
}
