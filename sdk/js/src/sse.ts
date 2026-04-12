import type { ChatCompletionChunk } from "./types.js";

/** Maximum SSE buffer size (1 MB) to prevent OOM from malicious servers. */
const MAX_BUFFER_SIZE = 1 * 1024 * 1024;

/** Maximum time (ms) to wait for the next SSE chunk before aborting. */
const READ_TIMEOUT_MS = 5 * 60 * 1000; // 5 minutes

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
 *
 * @param response - The fetch Response whose body will be streamed.
 * @param onParseError - Optional callback invoked when a chunk fails to parse.
 *   Receives the error and the raw data string. When omitted, malformed chunks
 *   are silently skipped.
 */
export async function* parseSSEStream(
  response: Response,
  onParseError?: (error: Error, rawData: string) => void
): AsyncGenerator<ChatCompletionChunk> {
  const reader = response.body?.getReader();
  if (!reader) throw new Error("Response body is not readable.");

  const decoder = new TextDecoder();
  let buffer = "";

  try {
    while (true) {
      const readPromise = reader.read();
      const timeout = new Promise<never>((_, reject) =>
        setTimeout(() => reject(new Error("SSE read timed out")), READ_TIMEOUT_MS)
      );
      const { done, value } = await Promise.race([readPromise, timeout]);
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
        } catch (err) {
          // Report malformed chunk via callback; skip if no handler provided.
          onParseError?.(
            err instanceof Error ? err : new Error(String(err)),
            data.length > 200 ? data.slice(0, 200) + "…" : data
          );
        }
      }
    }
  } finally {
    reader.releaseLock();
  }
}

export { MAX_BUFFER_SIZE, safeJsonParse };
