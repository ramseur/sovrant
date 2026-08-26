using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Python;

/// <summary>Phase 73 — Python FastAPI scaffold (uv, ruff, pytest, httpx).</summary>
public sealed class PythonFastApiScaffold : IProjectTemplate
{
    public string Id => "python/fastapi";
    public string Language => "python";
    public string Kind => "fastapi";
    public string Name => "Python FastAPI";
    public string Description => "Python FastAPI service with health endpoint, pytest + httpx integration tests, ruff linting, and uv packaging.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var snake = ScaffoldHelpers.ToSnakeCase(context.ProjectName);
        var kebab = ScaffoldHelpers.ToKebabCase(context.ProjectName);

        return
        [
            new("pyproject.toml", $$"""
                [project]
                name = "{{kebab}}"
                version = "0.1.0"
                requires-python = ">=3.12"
                dependencies = [
                    "fastapi>=0.115",
                    "uvicorn[standard]>=0.32",
                ]

                [project.optional-dependencies]
                dev = [
                    "httpx>=0.27",
                    "pytest>=8",
                    "pytest-asyncio>=0.24",
                    "ruff>=0.8",
                ]

                [tool.pytest.ini_options]
                asyncio_mode = "auto"

                [tool.ruff.lint]
                select = ["E", "F", "I", "UP"]
                """),

            new($"src/{snake}/__init__.py", ""),

            new($"src/{snake}/main.py", $$"""
                from fastapi import FastAPI

                app = FastAPI(title="{{kebab}}")


                @app.get("/health")
                async def health() -> dict[str, str]:
                    return {"status": "ok"}


                @app.get("/")
                async def root() -> dict[str, str]:
                    return {"message": "Hello from {{kebab}}!"}
                """),

            new("tests/__init__.py", ""),

            new("tests/test_health.py", $$"""
                import pytest
                from httpx import AsyncClient, ASGITransport
                from {{snake}}.main import app


                @pytest.mark.asyncio
                async def test_health_returns_ok():
                    async with AsyncClient(
                        transport=ASGITransport(app=app), base_url="http://test"
                    ) as client:
                        response = await client.get("/health")
                    assert response.status_code == 200
                    assert response.json() == {"status": "ok"}
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
                      - uses: actions/setup-python@v5
                        with:
                          python-version: '3.12'
                      - run: pip install -e ".[dev]"
                      - run: pytest
                      - run: ruff check .
                """),

            new(".gitignore", """
                __pycache__/
                *.pyc
                .venv/
                .env
                dist/
                *.egg-info/
                .pytest_cache/
                .ruff_cache/
                """),

            new("README.md", $$"""
                # {{kebab}}

                A Python FastAPI service.

                ## Setup

                ```bash
                uv venv
                uv pip install -e ".[dev]"
                ```

                ## Run

                ```bash
                uvicorn {{snake}}.main:app --reload
                # Open http://localhost:8000/docs
                ```

                ## Test

                ```bash
                pytest
                ```

                ## Lint

                ```bash
                ruff check .
                ```
                """),
        ];
    }
}
