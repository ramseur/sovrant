using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Python;

/// <summary>Phase 73 — Python script/CLI scaffold (uv, ruff, pytest).</summary>
public sealed class PythonScriptScaffold : IProjectTemplate
{
    public string Id => "python/script";
    public string Language => "python";
    public string Kind => "script";
    public string Name => "Python Script";
    public string Description => "Python CLI script with a pyproject.toml entry point, pytest tests, and ruff linting.";
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
                dependencies = []

                [project.scripts]
                {{kebab}} = "{{snake}}.main:main"

                [project.optional-dependencies]
                dev = [
                    "pytest>=8",
                    "ruff>=0.8",
                ]

                [tool.ruff.lint]
                select = ["E", "F", "I", "UP"]
                """),

            new($"src/{snake}/__init__.py", ""),

            new($"src/{snake}/main.py", $$"""
                import sys


                def greet(name: str) -> str:
                    return f"Hello, {name}!"


                def main() -> None:
                    name = sys.argv[1] if len(sys.argv) > 1 else "World"
                    print(greet(name))


                if __name__ == "__main__":
                    main()
                """),

            new("tests/__init__.py", ""),

            new("tests/test_main.py", $$"""
                from {{snake}}.main import greet


                def test_greet_returns_salutation():
                    assert greet("Sovrant") == "Hello, Sovrant!"


                def test_greet_defaults_work():
                    result = greet("World")
                    assert "World" in result
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

                A Python CLI script.

                ## Setup

                ```bash
                uv venv
                uv pip install -e ".[dev]"
                ```

                ## Run

                ```bash
                python -m {{snake}}.main World
                # or after install:
                {{kebab}} World
                ```

                ## Test

                ```bash
                pytest
                ```
                """),
        ];
    }
}
