using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Node;

/// <summary>Phase 73 — Node.js Express API scaffold (TypeScript, supertest, Vitest).</summary>
public sealed class NodeExpressApiScaffold : IProjectTemplate
{
    public string Id => "node/express-api";
    public string Language => "node";
    public string Kind => "express-api";
    public string Name => "Node.js Express API";
    public string Description => "TypeScript Express REST API with health endpoint, supertest integration tests, and Vitest.";
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
                    "dev": "tsx watch src/server.ts",
                    "start": "node dist/server.js",
                    "build": "tsc",
                    "test": "vitest run",
                    "test:watch": "vitest",
                    "typecheck": "tsc --noEmit"
                  },
                  "dependencies": {
                    "express": "^4.21.0"
                  },
                  "devDependencies": {
                    "@types/express": "^5.0.0",
                    "@types/node": "^22.0.0",
                    "@types/supertest": "^6.0.0",
                    "supertest": "^7.0.0",
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

            new("src/app.ts", """
                import express, { type Request, type Response } from 'express';

                export function createApp() {
                  const app = express();
                  app.use(express.json());

                  app.get('/health', (_req: Request, res: Response) => {
                    res.json({ status: 'ok' });
                  });

                  return app;
                }
                """),

            new("src/server.ts", """
                import { createApp } from './app.js';

                const app = createApp();
                const port = Number(process.env.PORT ?? 3000);

                app.listen(port, () => {
                  console.log(`Server listening on :${port}`);
                });
                """),

            new("src/app.test.ts", """
                import { describe, it, expect } from 'vitest';
                import request from 'supertest';
                import { createApp } from './app.js';

                describe('GET /health', () => {
                  it('returns 200 with status ok', async () => {
                    const app = createApp();
                    const res = await request(app).get('/health');
                    expect(res.status).toBe(200);
                    expect(res.body).toEqual({ status: 'ok' });
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

                A TypeScript Express REST API.

                ## Setup

                ```bash
                npm install
                ```

                ## Run

                ```bash
                npm run dev
                # Server starts at http://localhost:3000
                ```

                ## Test

                ```bash
                npm test
                ```

                ## Build

                ```bash
                npm run build
                npm start
                ```

                ## Endpoints

                | Method | Path | Description |
                |--------|------|-------------|
                | GET | /health | Health check |
                """),
        ];
    }
}
