using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Rust;

/// <summary>Phase 73 — Rust CLI scaffold (Cargo, clap, cargo test).</summary>
public sealed class RustCliScaffold : IProjectTemplate
{
    public string Id => "rust/cli";
    public string Language => "rust";
    public string Kind => "cli";
    public string Name => "Rust CLI";
    public string Description => "Rust CLI application with clap argument parsing, a library crate, and cargo test coverage.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var crate = ScaffoldHelpers.ToSnakeCase(context.ProjectName);
        var kebab = ScaffoldHelpers.ToKebabCase(context.ProjectName);

        return
        [
            new("Cargo.toml", $$"""
                [package]
                name = "{{kebab}}"
                version = "0.1.0"
                edition = "2021"

                [[bin]]
                name = "{{kebab}}"
                path = "src/main.rs"

                [lib]
                name = "{{crate}}"
                path = "src/lib.rs"

                [dependencies]
                clap = { version = "4", features = ["derive"] }

                [dev-dependencies]
                assert_cmd = "2"
                predicates = "3"
                """),

            new("src/lib.rs", $$"""
                /// Returns a greeting for the given name.
                pub fn greet(name: &str) -> String {
                    format!("Hello, {}!", name)
                }

                #[cfg(test)]
                mod tests {
                    use super::*;

                    #[test]
                    fn greet_returns_salutation() {
                        assert_eq!(greet("Sovrant"), "Hello, Sovrant!");
                    }

                    #[test]
                    fn greet_includes_name() {
                        let result = greet("World");
                        assert!(result.contains("World"));
                    }
                }
                """),

            new("src/main.rs", $$"""
                use clap::Parser;
                use {{crate}}::greet;

                #[derive(Parser)]
                #[command(name = "{{kebab}}", about = "{{kebab}} CLI")]
                struct Cli {
                    /// Name to greet
                    #[arg(default_value = "World")]
                    name: String,
                }

                fn main() {
                    let cli = Cli::parse();
                    println!("{}", greet(&cli.name));
                }
                """),

            new("tests/integration_test.rs", $$"""
                use assert_cmd::Command;
                use predicates::prelude::*;

                #[test]
                fn greets_default_name() {
                    Command::cargo_bin("{{kebab}}")
                        .unwrap()
                        .assert()
                        .success()
                        .stdout(predicate::str::contains("World"));
                }

                #[test]
                fn greets_given_name() {
                    Command::cargo_bin("{{kebab}}")
                        .unwrap()
                        .args(["Sovrant"])
                        .assert()
                        .success()
                        .stdout(predicate::str::contains("Sovrant"));
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
                      - uses: actions-rust-lang/setup-rust-toolchain@v1
                      - run: cargo build
                      - run: cargo test
                      - run: cargo clippy -- -D warnings
                """),

            new(".gitignore", """
                /target/
                .env
                """),

            new("README.md", $$"""
                # {{kebab}}

                A Rust CLI application.

                ## Run

                ```bash
                cargo run -- World
                ```

                ## Test

                ```bash
                cargo test
                ```

                ## Build (release)

                ```bash
                cargo build --release
                ./target/release/{{kebab}} World
                ```
                """),
        ];
    }
}
