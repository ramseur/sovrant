using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Go;

/// <summary>Phase 73 — Go HTTP API scaffold (net/http, standard library testing).</summary>
public sealed class GoApiScaffold : IProjectTemplate
{
    public string Id => "go/api";
    public string Language => "go";
    public string Kind => "api";
    public string Name => "Go HTTP API";
    public string Description => "Go HTTP API with net/http, health endpoint, and standard library tests.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pkg = ScaffoldHelpers.ToWordPackage(context.ProjectName);
        var kebab = ScaffoldHelpers.ToKebabCase(context.ProjectName);

        return
        [
            new("go.mod", $$"""
                module github.com/example/{{kebab}}

                go 1.23
                """),

            new("main.go", $$"""
                package main

                import (
                	"log/slog"
                	"net/http"

                	"github.com/example/{{kebab}}/handlers"
                )

                func main() {
                	mux := http.NewServeMux()
                	mux.HandleFunc("GET /health", handlers.Health)
                	mux.HandleFunc("GET /", handlers.Root)

                	slog.Info("server starting", "addr", ":8080")
                	if err := http.ListenAndServe(":8080", mux); err != nil {
                		slog.Error("server error", "err", err)
                	}
                }
                """),

            new("handlers/handlers.go", $$"""
                package handlers

                import (
                	"encoding/json"
                	"net/http"
                )

                func Health(w http.ResponseWriter, r *http.Request) {
                	w.Header().Set("Content-Type", "application/json")
                	json.NewEncoder(w).Encode(map[string]string{"status": "ok"}) //nolint:errcheck
                }

                func Root(w http.ResponseWriter, r *http.Request) {
                	w.Header().Set("Content-Type", "application/json")
                	json.NewEncoder(w).Encode(map[string]string{"message": "Hello from {{kebab}}!"}) //nolint:errcheck
                }
                """),

            new("handlers/handlers_test.go", $$"""
                package handlers_test

                import (
                	"encoding/json"
                	"net/http"
                	"net/http/httptest"
                	"testing"

                	"github.com/example/{{kebab}}/handlers"
                )

                func TestHealth(t *testing.T) {
                	req := httptest.NewRequest(http.MethodGet, "/health", nil)
                	w := httptest.NewRecorder()

                	handlers.Health(w, req)

                	if w.Code != http.StatusOK {
                		t.Errorf("expected 200, got %d", w.Code)
                	}

                	var body map[string]string
                	if err := json.Unmarshal(w.Body.Bytes(), &body); err != nil {
                		t.Fatalf("invalid JSON: %v", err)
                	}
                	if body["status"] != "ok" {
                		t.Errorf("expected status ok, got %q", body["status"])
                	}
                }
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
                      - uses: actions/setup-go@v5
                        with:
                          go-version: '1.23'
                      - run: go build ./...
                      - run: go test ./...
                      - run: go vet ./...
                """),

            new(".gitignore", """
                /bin/
                *.exe
                .env
                """),

            new("README.md", $$"""
                # {{kebab}}

                A Go HTTP API.

                ## Run

                ```bash
                go run .
                # Open http://localhost:8080/health
                ```

                ## Test

                ```bash
                go test ./...
                ```

                ## Build

                ```bash
                go build -o bin/{{kebab}} .
                ```
                """),
        ];
    }
}
