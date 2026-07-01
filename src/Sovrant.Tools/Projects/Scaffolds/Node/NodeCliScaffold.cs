using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Node;

/// <summary>Phase 73 — Node.js TypeScript CLI scaffold (tsx dev, tsc build, Vitest).</summary>
public sealed class NodeCliScaffold : IProjectTemplate
{
    public string Id => "node/cli";
    public string Language => "node";
    public string Kind => "cli";
    public string Name => "Node.js CLI";
    public string Description => "TypeScript command-line tool with tsx, a compiled build step, and Vitest tests.";
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
                  "scripts": {
                    "dev": "tsx src/main.ts",
                    "start": "node dist/main.js",
                    "build": "tsc",
                    "test": "vitest run",
                    "test:watch": "vitest",
                    "typecheck": "tsc --noEmit"
                  },
                  "devDependencies": {
                    "@types/node": "^22.0.0",
                    "tsx": "^4.19.0",
                    "typescript": "^5.7.0",
                    "vitest": "^3.0.0"
                  }
                }
                """),

            new("tsconfig.json", """
                {
                  "compilerOptions": {
                    "target": "ES2022",
                    "module": "NodeNext",
                    "moduleResolution": "NodeNext",
                    "outDir": "dist",
                    "rootDir": "src",
                    "strict": true,
                    "esModuleInterop": true
                  },
                  "include": ["src"],
                  "exclude": ["node_modules", "dist"]
                }
                """),

            new("src/lib.ts", $$"""
                export function greet(name: string): string {
                  return `Hello, ${name}!`;
                }
                """),

            new("src/main.ts", """
                import { greet } from './lib.js';

                const name = process.argv[2] ?? 'World';
                console.log(greet(name));
                """),

            new("src/lib.test.ts", """
                import { describe, it, expect } from 'vitest';
                import { greet } from './lib.js';

                describe('greet', () => {
                  it('greets by name', () => {
                    expect(greet('Sovrant')).toBe('Hello, Sovrant!');
                  });
                  it('defaults to World', () => {
                    expect(greet('World')).toBe('Hello, World!');
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
                .env.local
                *.log
                .DS_Store
                coverage/
                """),

            new("README.md", $$"""
                # {{name}}

                A TypeScript CLI tool.

                ## Setup

                ```bash
                npm install
                ```

                ## Run

                ```bash
                npm run dev -- World
                ```

                ## Test

                ```bash
                npm test
                ```

                ## Build

                ```bash
                npm run build
                node dist/main.js World
                ```
                """),
        ];
    }
}
