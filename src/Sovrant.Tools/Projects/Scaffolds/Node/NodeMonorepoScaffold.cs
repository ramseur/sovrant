using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Node;

/// <summary>Phase 73 — Node.js pnpm monorepo scaffold (TypeScript, Vitest, shared packages).</summary>
public sealed class NodeMonorepoScaffold : IProjectTemplate
{
    public string Id => "node/monorepo";
    public string Language => "node";
    public string Kind => "monorepo";
    public string Name => "Node.js Monorepo";
    public string Description => "pnpm workspace monorepo with TypeScript, shared packages, and Vitest.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var kebab = ScaffoldHelpers.ToKebabCase(context.ProjectName);
        var pascal = ScaffoldHelpers.ToPascalCase(context.ProjectName);

        return
        [
            new("pnpm-workspace.yaml", """
                packages:
                  - "packages/*"
                  - "apps/*"
                """),

            new("package.json", $$"""
                {
                  "name": "{{kebab}}",
                  "private": true,
                  "scripts": {
                    "build": "pnpm -r build",
                    "test": "pnpm -r test",
                    "lint": "pnpm -r lint",
                    "dev": "pnpm --filter @{{kebab}}/app dev"
                  },
                  "devDependencies": {
                    "typescript": "^5.7.3",
                    "vitest": "^2.1.8"
                  },
                  "engines": {
                    "node": ">=20",
                    "pnpm": ">=9"
                  }
                }
                """),

            new("tsconfig.base.json", """
                {
                  "compilerOptions": {
                    "strict": true,
                    "target": "ES2022",
                    "module": "NodeNext",
                    "moduleResolution": "NodeNext",
                    "declaration": true,
                    "declarationMap": true,
                    "sourceMap": true,
                    "esModuleInterop": true,
                    "skipLibCheck": true,
                    "composite": true
                  }
                }
                """),

            new($"packages/core/package.json", $$"""
                {
                  "name": "@{{kebab}}/core",
                  "version": "0.1.0",
                  "private": true,
                  "type": "module",
                  "main": "./dist/index.js",
                  "types": "./dist/index.d.ts",
                  "exports": {
                    ".": {
                      "import": "./dist/index.js",
                      "types": "./dist/index.d.ts"
                    }
                  },
                  "scripts": {
                    "build": "tsc -p tsconfig.json",
                    "test": "vitest run",
                    "lint": "tsc --noEmit"
                  },
                  "devDependencies": {
                    "typescript": "^5.7.3",
                    "vitest": "^2.1.8"
                  }
                }
                """),

            new($"packages/core/tsconfig.json", """
                {
                  "extends": "../../tsconfig.base.json",
                  "compilerOptions": {
                    "outDir": "./dist",
                    "rootDir": "./src"
                  },
                  "include": ["src"]
                }
                """),

            new($"packages/core/src/index.ts", $$"""
                export function greet(name: string): string {
                  return `Hello, ${name}!`;
                }
                """),

            new($"packages/core/src/index.test.ts", $$"""
                import { describe, it, expect } from "vitest";
                import { greet } from "./index.js";

                describe("greet", () => {
                  it("returns a salutation", () => {
                    expect(greet("Sovrant")).toBe("Hello, Sovrant!");
                  });

                  it("includes the name", () => {
                    expect(greet("World")).toContain("World");
                  });
                });
                """),

            new($"apps/app/package.json", $$"""
                {
                  "name": "@{{kebab}}/app",
                  "version": "0.1.0",
                  "private": true,
                  "type": "module",
                  "scripts": {
                    "dev": "node --import tsx/esm src/main.ts",
                    "build": "tsc -p tsconfig.json",
                    "test": "vitest run",
                    "lint": "tsc --noEmit"
                  },
                  "dependencies": {
                    "@{{kebab}}/core": "workspace:*"
                  },
                  "devDependencies": {
                    "tsx": "^4.19.2",
                    "typescript": "^5.7.3",
                    "vitest": "^2.1.8"
                  }
                }
                """),

            new($"apps/app/tsconfig.json", """
                {
                  "extends": "../../tsconfig.base.json",
                  "compilerOptions": {
                    "outDir": "./dist",
                    "rootDir": "./src"
                  },
                  "include": ["src"]
                }
                """),

            new($"apps/app/src/main.ts", $$"""
                import { greet } from "@{{kebab}}/core";

                const name = process.argv[2] ?? "World";
                console.log(greet(name));
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
                      - uses: pnpm/action-setup@v4
                        with:
                          version: 9
                      - uses: actions/setup-node@v4
                        with:
                          node-version: '22'
                          cache: 'pnpm'
                      - run: pnpm install
                      - run: pnpm build
                      - run: pnpm test
                """),

            new(".gitignore", """
                node_modules/
                dist/
                .pnpm-store/
                *.tsbuildinfo
                .env
                """),

            new("README.md", $$"""
                # {{kebab}}

                A pnpm workspace monorepo with TypeScript.

                ## Setup

                ```bash
                pnpm install
                ```

                ## Build

                ```bash
                pnpm build
                ```

                ## Run

                ```bash
                pnpm --filter @{{kebab}}/app dev -- World
                ```

                ## Test

                ```bash
                pnpm test
                ```

                ## Structure

                ```
                packages/
                  core/       # shared library
                apps/
                  app/        # main application
                ```
                """),
        ];
    }
}
