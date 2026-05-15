import { describe, it, expect } from "vitest";
import { parseSSEStream, MAX_BUFFER_SIZE, safeJsonParse } from "../src/sse.js";
import type { ChatCompletionChunk } from "../src/types.js";

// ─── Helpers ─────────────────────────────────────────────────────────────────

/** Create a mock Response with the given SSE text body. */
function mockSSEResponse(body: string): Response {
  const encoder = new TextEncoder();
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      controller.enqueue(encoder.encode(body));
      controller.close();
    },
  });
  return new Response(stream);
}

/** Create a mock Response that streams chunks one at a time. */
function mockChunkedSSEResponse(chunks: string[]): Response {
  const encoder = new TextEncoder();
  let i = 0;
  const stream = new ReadableStream<Uint8Array>({
    pull(controller) {
      if (i < chunks.length) {
        controller.enqueue(encoder.encode(chunks[i]));
        i++;
      } else {
        controller.close();
      }
    },
  });
  return new Response(stream);
}

/** Collect all chunks from the async generator. */
async function collectChunks(
  response: Response
): Promise<ChatCompletionChunk[]> {
  const chunks: ChatCompletionChunk[] = [];
  for await (const chunk of parseSSEStream(response)) {
    chunks.push(chunk);
  }
  return chunks;
}

// ─── Prototype Pollution ─────────────────────────────────────────────────────

describe("SSE — prototype pollution prevention", () => {
  it("strips __proto__ key from parsed JSON", () => {
    const malicious = '{"id":"1","__proto__":{"polluted":true},"object":"chunk","model":"m","choices":[]}';
    const result = safeJsonParse(malicious);
    expect(result.id).toBe("1");
    // __proto__ should be stripped
    expect((result as Record<string, unknown>).__proto__).toBe(
      Object.getPrototypeOf(result)
    );
    // Verify the global Object prototype was not polluted
    const clean: Record<string, unknown> = {};
    expect(clean["polluted"]).toBeUndefined();
  });

  it("strips constructor key from parsed JSON", () => {
    const malicious =
      '{"id":"1","constructor":{"prototype":{"pwned":true}},"object":"chunk","model":"m","choices":[]}';
    const result = safeJsonParse(malicious);
    // constructor should be stripped (returns undefined for the key)
    expect((result as Record<string, unknown>)["constructor"]).toBeUndefined();
    // Global Object not polluted
    const clean: Record<string, unknown> = {};
    expect(clean["pwned"]).toBeUndefined();
  });

  it("strips nested __proto__ in SSE stream", async () => {
    const payload = JSON.stringify({
      id: "1",
      object: "chat.completion.chunk",
      model: "test",
      choices: [
        {
          index: 0,
          delta: { content: "hi", __proto__: { evil: true } },
          finish_reason: null,
        },
      ],
    });
    // Manually construct since JSON.stringify won't include __proto__ normally
    const raw = payload.replace(
      '"content":"hi"',
      '"content":"hi","__proto__":{"evil":true}'
    );
    const response = mockSSEResponse(`data: ${raw}\n\n`);
    const chunks = await collectChunks(response);
    expect(chunks).toHaveLength(1);
    // Global prototype not polluted
    const clean: Record<string, unknown> = {};
    expect(clean["evil"]).toBeUndefined();
  });
});

// ─── Malformed SSE Data ──────────────────────────────────────────────────────

describe("SSE — malformed data resilience", () => {
  it("skips non-JSON data lines", async () => {
    const body = [
      'data: <script>alert("xss")</script>',
      "",
      'data: {"id":"1","object":"c","model":"m","choices":[]}',
      "",
      "",
    ].join("\n");
    const chunks = await collectChunks(mockSSEResponse(body));
    expect(chunks).toHaveLength(1);
    expect(chunks[0].id).toBe("1");
  });

  it("skips extremely deeply nested JSON", async () => {
    // Create deeply nested object — JSON.parse may handle it but shouldn't crash
    let deep = '{"id":"1","object":"c","model":"m","choices":[],"nested":';
    for (let i = 0; i < 100; i++) deep += '{"a":';
    deep += "1";
    for (let i = 0; i < 100; i++) deep += "}";
    deep += "}";
    const body = `data: ${deep}\n\n`;
    // Should not throw — either parses or skips
    const chunks = await collectChunks(mockSSEResponse(body));
    expect(chunks.length).toBeLessThanOrEqual(1);
  });

  it("handles empty data lines gracefully", async () => {
    const body = "data: \n\ndata: \n\n";
    const chunks = await collectChunks(mockSSEResponse(body));
    expect(chunks).toHaveLength(0);
  });

  it("handles data: [DONE] correctly", async () => {
    const body = [
      'data: {"id":"1","object":"c","model":"m","choices":[]}',
      "",
      "data: [DONE]",
      "",
      'data: {"id":"2","object":"c","model":"m","choices":[]}',
      "",
      "",
    ].join("\n");
    const chunks = await collectChunks(mockSSEResponse(body));
    expect(chunks).toHaveLength(1);
    expect(chunks[0].id).toBe("1");
  });

  it("ignores SSE comment lines", async () => {
    const body = [
      ": this is a comment",
      "",
      'data: {"id":"1","object":"c","model":"m","choices":[]}',
      "",
      "",
    ].join("\n");
    const chunks = await collectChunks(mockSSEResponse(body));
    expect(chunks).toHaveLength(1);
  });

  it("ignores event: and id: lines", async () => {
    const body = [
      "event: message",
      "id: 42",
      'data: {"id":"1","object":"c","model":"m","choices":[]}',
      "",
      "",
    ].join("\n");
    const chunks = await collectChunks(mockSSEResponse(body));
    expect(chunks).toHaveLength(1);
  });

  it("handles null bytes in data", async () => {
    const body = 'data: {"id":"1\x00","object":"c","model":"m","choices":[]}\n\n';
    // Should parse (null byte is valid in JSON string) or skip — not crash
    const chunks = await collectChunks(mockSSEResponse(body));
    expect(chunks.length).toBeLessThanOrEqual(1);
  });

  it("handles unicode escape sequences", async () => {
    const body =
      'data: {"id":"\\u0031","object":"c","model":"m","choices":[]}\n\n';
    const chunks = await collectChunks(mockSSEResponse(body));
    expect(chunks).toHaveLength(1);
  });
});

// ─── Buffer Overflow Protection ──────────────────────────────────────────────

describe("SSE — buffer size limit", () => {
  it("throws when buffer exceeds MAX_BUFFER_SIZE", async () => {
    // Create a stream that sends data without line breaks to grow the buffer
    const bigChunk = "x".repeat(MAX_BUFFER_SIZE + 1);
    const response = mockSSEResponse(bigChunk);

    await expect(collectChunks(response)).rejects.toThrow(
      /SSE buffer exceeded maximum size/
    );
  });

  it("does not throw for valid large-but-bounded messages", async () => {
    // Create a large but properly delimited SSE message (under the limit)
    const largeContent = "a".repeat(10000);
    const chunk = JSON.stringify({
      id: "1",
      object: "c",
      model: "m",
      choices: [{ index: 0, delta: { content: largeContent }, finish_reason: null }],
    });
    const body = `data: ${chunk}\n\ndata: [DONE]\n\n`;
    const chunks = await collectChunks(mockSSEResponse(body));
    expect(chunks).toHaveLength(1);
    expect(chunks[0].choices[0].delta.content).toBe(largeContent);
  });
});

// ─── Chunked Delivery ────────────────────────────────────────────────────────

describe("SSE — cross-chunk boundary handling", () => {
  it("handles JSON split across multiple chunks", async () => {
    const full = '{"id":"1","object":"c","model":"m","choices":[]}';
    const response = mockChunkedSSEResponse([
      `data: ${full.slice(0, 10)}`,
      `${full.slice(10)}\n\n`,
    ]);
    const chunks = await collectChunks(response);
    expect(chunks).toHaveLength(1);
    expect(chunks[0].id).toBe("1");
  });

  it("handles delimiter split across chunks", async () => {
    const json = '{"id":"1","object":"c","model":"m","choices":[]}';
    const response = mockChunkedSSEResponse([
      `data: ${json}\n`,
      `\ndata: [DONE]\n\n`,
    ]);
    const chunks = await collectChunks(response);
    expect(chunks).toHaveLength(1);
  });
});

// ─── Edge Cases ──────────────────────────────────────────────────────────────

describe("SSE — edge cases", () => {
  it("throws on Response with no body", async () => {
    // Create a Response-like object with null body
    const response = { body: null } as Response;
    await expect(collectChunks(response)).rejects.toThrow(
      /not readable/
    );
  });

  it("handles empty stream", async () => {
    const response = mockSSEResponse("");
    const chunks = await collectChunks(response);
    expect(chunks).toHaveLength(0);
  });

  it("handles stream with only whitespace", async () => {
    const response = mockSSEResponse("   \n\n   \n\n");
    const chunks = await collectChunks(response);
    expect(chunks).toHaveLength(0);
  });
});
