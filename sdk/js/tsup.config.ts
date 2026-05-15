import { defineConfig } from "tsup";

export default defineConfig({
  // Both public entry points.
  entry: {
    index: "src/index.ts",
    "hooks/use-chat": "src/hooks/use-chat.ts",
  },

  // ESM only — matches package.json "type": "module".
  format: ["esm"],

  // Match the TypeScript target already used in the project.
  target: "es2022",

  // Generate .d.ts declaration files for IDE / TypeScript consumers.
  // Type definitions are not minified — they carry no runtime logic.
  dts: true,

  // No source maps in the published dist.
  // Source maps would map minified names back to originals, undoing the minification.
  // For your own error tracking, generate them separately and upload to Sentry/similar.
  sourcemap: false,

  // esbuild minification — strips whitespace, shortens identifiers, removes dead code.
  minify: true,

  // Wipe dist/ before each build so stale files never ship.
  clean: true,

  // One file per entry point — no dynamic chunk splitting.
  splitting: false,

  // Remove exports/imports that are never used in the output.
  treeshake: true,

  // React is a peer dependency — don't bundle it, let the consumer provide it.
  external: ["react"],
});
