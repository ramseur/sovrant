using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Node;

/// <summary>Phase 73 — Node.js TypeScript library scaffold (tsup dual-CJS/ESM, Vitest).</summary>
public sealed class NodeLibraryScaffold : IProjectTemplate
{
    public string Id => "node/library";
    public string Language => "node";
    public string Kind => "library";
    public string Name => "Node.js Library";
    public string Description => "TypeScript library with tsup (dual CJS + ESM output), type declarations, and Vitest tests.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var name = ScaffoldHelpers.ToKebabCase(context.ProjectName);

        return
        [
            new("package.json", $$"""
                {
                  "name": "{{name}}",
                  "version": "0.1.0",
                  "type": "module",
                  "main": "./dist/index.cjs",
                  "module": "./dist/index.js",
                  "types": "./dist/index.d.ts",
                  "exports": {
                    ".": {
                      "import": "./dist/index.js",
                      "require": "./dist/index.cjs",
                      "types": "./dist/index.d.ts"
                    }
                  },
                  "files": ["dist"],
                  "scripts": {
                    "build": "tsup",
                    "build:dev": "tsup --no-minify --sourcemap",
                    "test": "vitest run",
                    "test:watch": "vitest",
                    "typecheck": "tsc --noEmit"
                  },
                  "devDependencies": {
                    "tsup": "^8.0.0",
                    "typescript": "^5.7.0",
                    "vitest": "^3.0.0"
                  }
                }
                """),

            new("tsconfig.json", """
                {
                  "compilerOptions": {
                    "target": "ES2022",
                    "module": "ESNext",
                    "moduleResolution": "Bundler",
                    "declaration": true,
                    "declarationDir": "dist",
                    "outDir": "dist",
                    "rootDir": "src",
                    "strict": true,
                    "esModuleInterop": true
                  },
                  "include": ["src"],
                  "exclude": ["node_modules", "dist"]
                }
                """),

            new("tsup.config.ts", $$"""
                import { defineConfig } from 'tsup';

                export default defineConfig({
                  entry: ['src/index.ts'],
                  format: ['esm', 'cjs'],
                  dts: true,
                  clean: true,
                  minify: false,
                });
                """),

            new("src/index.ts", $$"""
                export function greet(name: string): string {
                  return `Hello from {{name}}, ${name}!`;
                }
                """),

            new("src/index.test.ts", $$"""
                import { describe, it, expect } from 'vitest';
                import { greet } from './index.js';

                describe('greet', () => {
                  it('returns a greeting string', () => {
                    const result = greet('world');
                    expect(result).toContain('world');
                  });
                });
                """),

            new(".github/workflows/ci.yml", """
                name: CI

                on:
                  push:
                    branches: [main, master]
                  pull_request:
                    branches: [main, master]

                jobs:
                  build-and-test:
                    runs-on: ubuntu-latest
                    steps:
                      - uses: actions/checkout@v4
                      - uses: actions/setup-node@v4
                        with:
                          node-version: '22'
                          cache: 'npm'
                      - run: npm install
                      - run: npm test
                      - run: npm run build
                """),

            new(".gitignore", """
                node_modules/
                dist/
                .env
                *.log
                .DS_Store
                coverage/
                """),

            new("README.md", $$"""
                # {{name}}

                A TypeScript library.

                ## Install

                ```bash
                npm install {{name}}
                ```

                ## Usage

                ```ts
                import { greet } from '{{name}}';

                console.log(greet('World'));
                ```

                ## Development

                ```bash
                npm install
                npm test
                npm run build
                ```
                """),
        ];
    }
}
