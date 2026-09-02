using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Node;

/// <summary>Phase 73 — Next.js 15 App Router scaffold (TypeScript, Tailwind, Vitest).</summary>
public sealed class NodeNextJsScaffold : IProjectTemplate
{
    public string Id => "node/nextjs";
    public string Language => "node";
    public string Kind => "nextjs";
    public string Name => "Next.js App";
    public string Description => "Next.js 15 App Router with TypeScript, Tailwind CSS, and Vitest component tests.";
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
                  "private": true,
                  "scripts": {
                    "dev": "next dev",
                    "build": "next build",
                    "start": "next start",
                    "lint": "next lint",
                    "test": "vitest run",
                    "test:watch": "vitest"
                  },
                  "dependencies": {
                    "next": "^15.0.0",
                    "react": "^19.0.0",
                    "react-dom": "^19.0.0"
                  },
                  "devDependencies": {
                    "@testing-library/react": "^16.0.0",
                    "@testing-library/user-event": "^14.0.0",
                    "@types/node": "^22.0.0",
                    "@types/react": "^19.0.0",
                    "@types/react-dom": "^19.0.0",
                    "@vitejs/plugin-react": "^4.0.0",
                    "autoprefixer": "^10.0.0",
                    "jsdom": "^26.0.0",
                    "postcss": "^8.0.0",
                    "tailwindcss": "^3.0.0",
                    "typescript": "^5.7.0",
                    "vitest": "^3.0.0"
                  }
                }
                """),

            new("tsconfig.json", """
                {
                  "compilerOptions": {
                    "target": "ES2022",
                    "lib": ["dom", "dom.iterable", "esnext"],
                    "allowJs": true,
                    "skipLibCheck": true,
                    "strict": true,
                    "noEmit": true,
                    "esModuleInterop": true,
                    "module": "esnext",
                    "moduleResolution": "bundler",
                    "resolveJsonModule": true,
                    "isolatedModules": true,
                    "jsx": "preserve",
                    "incremental": true,
                    "plugins": [{ "name": "next" }],
                    "paths": { "@/*": ["./src/*"] }
                  },
                  "include": ["next-env.d.ts", "**/*.ts", "**/*.tsx", ".next/types/**/*.ts"],
                  "exclude": ["node_modules"]
                }
                """),

            new("next.config.ts", """
                import type { NextConfig } from 'next';

                const config: NextConfig = {};

                export default config;
                """),

            new("vitest.config.ts", """
                import { defineConfig } from 'vitest/config';
                import react from '@vitejs/plugin-react';

                export default defineConfig({
                  plugins: [react()],
                  test: {
                    environment: 'jsdom',
                    globals: true,
                  },
                });
                """),

            new("postcss.config.js", """
                module.exports = {
                  plugins: { tailwindcss: {}, autoprefixer: {} },
                };
                """),

            new("tailwind.config.ts", """
                import type { Config } from 'tailwindcss';

                const config: Config = {
                  content: ['./src/**/*.{ts,tsx}'],
                  theme: { extend: {} },
                  plugins: [],
                };

                export default config;
                """),

            new("src/app/layout.tsx", $$"""
                import type { Metadata } from 'next';
                import './globals.css';

                export const metadata: Metadata = {
                  title: '{{name}}',
                  description: 'Built with Next.js',
                };

                export default function RootLayout({ children }: { children: React.ReactNode }) {
                  return (
                    <html lang="en">
                      <body>{children}</body>
                    </html>
                  );
                }
                """),

            new("src/app/globals.css", """
                @tailwind base;
                @tailwind components;
                @tailwind utilities;
                """),

            new("src/app/page.tsx", $$"""
                export default function Home() {
                  return (
                    <main className="flex min-h-screen flex-col items-center justify-center p-24">
                      <h1 className="text-4xl font-bold">{{name}}</h1>
                      <p className="mt-4 text-gray-600">Get started by editing src/app/page.tsx</p>
                    </main>
                  );
                }
                """),

            new("src/app/page.test.tsx", """
                import { describe, it, expect } from 'vitest';
                import { render, screen } from '@testing-library/react';
                import Home from './page';

                describe('Home page', () => {
                  it('renders without crashing', () => {
                    render(<Home />);
                    expect(document.body).toBeTruthy();
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
                .next/
                out/
                dist/
                .env
                .env.local
                .env.development.local
                .env.test.local
                .env.production.local
                *.log
                .DS_Store
                coverage/
                """),

            new("README.md", $$"""
                # {{name}}

                A Next.js 15 App Router application.

                ## Setup

                ```bash
                npm install
                ```

                ## Run

                ```bash
                npm run dev
                # Open http://localhost:3000
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
                """),
        ];
    }
}
