import type { ChatCompletionChunk } from "./types.js";

/** Maximum SSE buffer size (10 MB) to prevent OOM from malicious servers. */
const MAX_BUFFER_SIZE = 10 * 1024 * 1024;

/**
 * Safely parse JSON, stripping dangerous __proto__ and constructor keys
 * to prevent prototype pollution attacks.
 */
function safeJsonParse(data: string): ChatCompletionChunk {
  return JSON.parse(data, (key, value) => {
    if (key === "__proto__" || key === "constructor" || key === "prototype") {
      return undefined;
    }
    return value as unknown;
  }) as ChatCompletionChunk;
}

/**
 * Parses a Server-Sent Events stream from a fetch Response into an async
 * iterable of ChatCompletionChunk objects.
 *
 * Handles partial lines across chunk boundaries and stops on `data: [DONE]`.
 * Enforces a maximum buffer size to guard against unbounded memory growth.
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

      // Guard against unbounded buffer growth (malicious server).
      if (buffer.length > MAX_BUFFER_SIZE) {
        throw new Error(
          `SSE buffer exceeded maximum size of ${MAX_BUFFER_SIZE} bytes. ` +
            "This may indicate a malicious or misbehaving server."
        );
      }

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
          yield safeJsonParse(data);
        } catch {
          // Skip malformed chunks.
        }
      }
    }
  } finally {
    reader.releaseLock();
  }
}

export { MAX_BUFFER_SIZE, safeJsonParse };
