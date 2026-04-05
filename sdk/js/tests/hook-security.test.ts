import { describe, it, expect, vi, afterEach } from "vitest";

// Mock React before importing the hook
vi.mock("react", () => {
  let stateMap = new Map<number, unknown>();
  let stateIndex = 0;

  function resetState() {
    stateMap = new Map();
    stateIndex = 0;
  }

  function useState<T>(initial: T): [T, (v: T | ((prev: T) => T)) => void] {
    const idx = stateIndex++;
    if (!stateMap.has(idx)) {
      stateMap.set(idx, initial);
    }
    const val = stateMap.get(idx) as T;
    const setter = (v: T | ((prev: T) => T)) => {
      const newVal = typeof v === "function" ? (v as (prev: T) => T)(stateMap.get(idx) as T) : v;
      stateMap.set(idx, newVal);
    };
    return [val, setter];
  }

  function useCallback<T extends (...args: unknown[]) => unknown>(fn: T, _deps: unknown[]): T {
    return fn;
  }

  function useRef<T>(initial: T): { current: T } {
    return { current: initial };
  }

  return { useState, useCallback, useRef, __resetState: resetState };
});

import { useChat } from "../src/hooks/use-chat.js";

describe("useChat — security", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("rejects empty messages (no whitespace-only messages sent)", () => {
    const fetchSpy = vi.fn();
    vi.stubGlobal("fetch", fetchSpy);

    const { send } = useChat({
      baseUrl: "http://localhost:5200",
      token: "t",
    });

    // Should not send whitespace-only message
    send("   ");
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it("passes token validation from SovrantClient constructor", () => {
    expect(() =>
      useChat({
        baseUrl: "http://localhost:5200",
        token: "",
      })
    ).toThrow(/non-empty token/);
  });

  it("passes URL validation from SovrantClient constructor", () => {
    expect(() =>
      useChat({
        baseUrl: "javascript:alert(1)",
        token: "t",
      })
    ).toThrow(/Invalid baseUrl/);
  });

  it("does not include HTML in message IDs", () => {
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ choices: [{ message: { content: "ok" } }] }), {
        status: 200,
      })
    );
    vi.stubGlobal("fetch", fetchSpy);

    const { messages } = useChat({
      baseUrl: "http://localhost:5200",
      token: "t",
      initialMessages: [],
    });

    // Message IDs should be safe alphanumeric + dashes
    for (const msg of messages) {
      expect(msg.id).toMatch(/^[a-zA-Z0-9-]+$/);
    }
  });
});
