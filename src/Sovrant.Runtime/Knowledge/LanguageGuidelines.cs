namespace Sovrant.Runtime.Knowledge;

/// <summary>Built-in language guideline for code generation conventions.</summary>
public sealed record BuiltInGuideline(string Slug, string Name, string Description, string Body);

/// <summary>
/// Built-in coding conventions and generation hints for each supported language.
/// These are the defaults; workspace admins can create per-slug user overrides via
/// the Knowledge → Code Templates UI, which are stored in the DB and take precedence.
/// </summary>
public static class LanguageGuidelines
{
    public static readonly IReadOnlyList<BuiltInGuideline> All = new BuiltInGuideline[]
    {
        new("node",
            "Node.js / TypeScript Guidelines",
            "Conventions and patterns for Node.js projects using TypeScript.",
            """
            ## Conventions
            - TypeScript strict mode (`"strict": true` in tsconfig.json)
            - ESLint with `@typescript-eslint` rules + Prettier for formatting
            - Named exports over default exports in library code
            - `camelCase` for variables/functions, `PascalCase` for types/classes
            - File names: `kebab-case.ts`

            ## Preferred Patterns
            - `async/await` over raw Promises or callbacks
            - Dependency injection via constructor parameters (no global singletons)
            - Zod for runtime schema validation at API boundaries
            - Result/Either types for error propagation in domain logic (avoid throw for control flow)

            ## Dependency Standards
            - Runtime: prefer Node built-ins + stdlib before adding packages
            - HTTP clients: `node-fetch` or native `fetch` (Node 18+); avoid `axios` unless already present
            - Testing: Vitest (fast, ESM-native); Jest is acceptable for existing projects
            - Logging: `pino` for structured JSON logs; avoid `console.log` in library code

            ## Generation Hints
            - Target Node.js 20 LTS minimum unless the scaffold sets a different version
            - Include a `.nvmrc` with the target Node version
            - Use `type: "module"` in `package.json` for new projects (ESM-first)
            - Scaffold `src/` for source, `tests/` for tests, `dist/` in `.gitignore`
            - Add `engines.node` field in `package.json`
            """),

        new("dotnet",
            ".NET / C# Guidelines",
            "Conventions and patterns for .NET projects using C#.",
            """
            ## Conventions
            - Nullable reference types enabled (`<Nullable>enable</Nullable>`)
            - File-scoped namespaces (`namespace Foo.Bar;`)
            - `PascalCase` for types/properties/methods, `_camelCase` for private fields
            - Records for immutable DTOs; `sealed` classes by default unless designed for inheritance
            - `var` for local variables when the type is obvious from the right-hand side

            ## Preferred Patterns
            - `async`/`await` throughout; never use `.Result` or `.Wait()` on Tasks
            - Constructor injection via Microsoft.Extensions.DependencyInjection
            - `ILogger<T>` for structured logging (no `Console.Write`)
            - `CancellationToken` as the last parameter on every async method
            - `ConfigureAwait(false)` in library code; not required in ASP.NET Core handlers

            ## Dependency Standards
            - Test framework: xUnit + FluentAssertions; Moq or NSubstitute for mocking
            - JSON: `System.Text.Json` (built-in); avoid Newtonsoft unless legacy project
            - HTTP: `HttpClient` via `IHttpClientFactory`; avoid custom `HttpClient` instances
            - ORM: Entity Framework Core for full projects; Dapper for read-heavy CQRS slices

            ## Generation Hints
            - Target `net10.0` (or latest LTS) unless scaffold specifies otherwise
            - Use top-level program (`Program.cs` without explicit `Main`) for entry points
            - Apply `[GeneratedRegex]` for static regex; avoid `new Regex(...)` without caching
            - Place each public type in its own file matching the type name
            - Add `.editorconfig` with standard C# rules to all new projects
            """),

        new("python",
            "Python Guidelines",
            "Conventions and patterns for Python projects.",
            """
            ## Conventions
            - Python 3.11+ minimum; use type hints on all public functions and class members
            - PEP 8 style; format with `ruff format` (or `black`) + `ruff check` for linting
            - `snake_case` for variables/functions, `PascalCase` for classes, `UPPER_CASE` for constants
            - Prefer dataclasses or Pydantic models over plain dicts for structured data
            - Keep modules small; avoid circular imports

            ## Preferred Patterns
            - `asyncio` + `async/await` for I/O-bound code (FastAPI, aiohttp, etc.)
            - Context managers (`with`) for resources; implement `__enter__`/`__exit__` or use `contextlib`
            - `pathlib.Path` over `os.path` for all filesystem operations
            - Explicit exception types; never `except Exception` without re-raising
            - Logging via `logging.getLogger(__name__)`; no `print()` in library code

            ## Dependency Standards
            - Package management: `uv` (preferred) or `pip` + `pyproject.toml`
            - Testing: `pytest` + `pytest-asyncio` for async tests
            - HTTP: `httpx` for async clients; `requests` for sync scripts
            - Validation: `pydantic` v2 for API schemas and config
            - Environment: `.env` files loaded with `python-dotenv`

            ## Generation Hints
            - Include `pyproject.toml` (PEP 517) rather than `setup.py`
            - Add `ruff.toml` or `[tool.ruff]` section in `pyproject.toml`
            - Scaffold `src/<package>/` layout for libraries; flat layout for scripts
            - Include `py.typed` marker file for typed libraries
            - Pin Python version in `.python-version` (pyenv) and `pyproject.toml` requires-python
            """),

        new("go",
            "Go Guidelines",
            "Conventions and patterns for Go projects.",
            """
            ## Conventions
            - Go modules (`go.mod`); module path matches repository URL
            - `gofmt` + `goimports` for formatting (CI must enforce)
            - Short, lowercase package names; avoid stutter (`user.User` → `user.Record`)
            - Exported identifiers have doc comments starting with the identifier name
            - Error variables: `var ErrNotFound = errors.New("not found")` at package level

            ## Preferred Patterns
            - Errors are values; return `(T, error)` — never panic for control flow
            - Wrap errors with context: `fmt.Errorf("loading config: %w", err)`
            - Interfaces defined at the point of use (consumer side), not declaration side
            - Channels and goroutines for concurrency; use `context.Context` for cancellation
            - Table-driven tests with `t.Run` sub-tests

            ## Dependency Standards
            - Standard library first; add external deps only when stdlib is insufficient
            - HTTP router: `chi` or `net/http` stdlib for new services
            - Database: `database/sql` + `pgx` driver; `sqlx` for scan convenience
            - Testing: stdlib `testing` + `testify/assert` for assertions

            ## Generation Hints
            - Use `cmd/<binary>/main.go` layout for executables
            - `internal/` for private packages, `pkg/` for public library code
            - Always include a `Makefile` with `build`, `test`, `lint` targets
            - Add `golangci-lint` config (`.golangci.yml`) to enforce style
            - Use `//go:generate` directives for code generation (mocks, embeddings)
            """),

        new("rust",
            "Rust Guidelines",
            "Conventions and patterns for Rust projects.",
            """
            ## Conventions
            - Rust 2021 edition (`edition = "2021"` in `Cargo.toml`)
            - `rustfmt` for formatting; `clippy` with `deny(clippy::all)` in CI
            - `snake_case` everywhere except types (`PascalCase`) and constants (`SCREAMING_SNAKE_CASE`)
            - Prefer `impl Trait` in function signatures over explicit generic bounds where possible
            - Document all public items with `///` doc comments

            ## Preferred Patterns
            - `Result<T, E>` for fallible operations; use `?` operator for propagation
            - `thiserror` for defining error types; `anyhow` for application error handling
            - `tokio` runtime for async I/O; avoid mixing async runtimes
            - Newtype pattern for domain primitives (`struct UserId(Uuid)`)
            - Builder pattern for complex struct construction

            ## Dependency Standards
            - Async runtime: `tokio` with `full` feature for services; `smol` for simpler needs
            - Serialization: `serde` + `serde_json`
            - HTTP: `axum` for services; `reqwest` for HTTP clients
            - Testing: built-in `#[test]` + `#[tokio::test]` for async; `proptest` for property tests
            - Logging: `tracing` for structured, context-aware logs

            ## Generation Hints
            - Organize as a Cargo workspace for multi-crate projects
            - Use `src/lib.rs` + `src/main.rs` split: library logic in `lib.rs`, thin CLI in `main.rs`
            - Add `rust-toolchain.toml` to pin the Rust version
            - Configure `deny.toml` for supply-chain security checks (`cargo-deny`)
            - Include `benches/` directory for criterion benchmarks if performance is critical
            """),

        new("java",
            "Java Guidelines",
            "Conventions and patterns for Java projects.",
            """
            ## Conventions
            - Java 21 LTS minimum; use records, sealed classes, and pattern matching where appropriate
            - Google Java Style or Checkstyle with project-specific ruleset
            - `PascalCase` for classes/interfaces, `camelCase` for methods/variables, `SCREAMING_SNAKE_CASE` for constants
            - Prefer immutability; use `final` fields and defensive copies
            - Annotations over XML configuration (Spring, JPA)

            ## Preferred Patterns
            - Spring Boot 3.x for web services; use constructor injection (not field injection)
            - Virtual threads (Project Loom, Java 21) for I/O-bound workloads
            - `Optional<T>` at API boundaries; never return `null` from public methods
            - Structured logging with SLF4J + Logback; use MDC for request context
            - JUnit 5 + AssertJ for tests; Mockito for mocking

            ## Dependency Standards
            - Build tool: Maven (standard) or Gradle Kotlin DSL
            - Spring Boot 3.x BOM for dependency management in Spring projects
            - Lombok for reducing boilerplate (or use Java records for DTOs)
            - Database: Spring Data JPA + Hibernate, or jOOQ for complex query projects
            - Validation: Jakarta Bean Validation (`@NotNull`, `@Valid`)

            ## Generation Hints
            - Package structure: `com.<org>.<project>.<layer>` (controller, service, repository, model)
            - Include `application.yml` rather than `application.properties`
            - Add `Dockerfile` with multi-stage build (builder + runtime image)
            - Configure Maven Wrapper (`mvnw`) for reproducible builds
            - Include a `.editorconfig` to enforce consistent indentation
            """),

        new("kotlin",
            "Kotlin Guidelines",
            "Conventions and patterns for Kotlin projects.",
            """
            ## Conventions
            - Kotlin 1.9+ / 2.0; use the Kotlin Gradle DSL (`build.gradle.kts`)
            - `PascalCase` for classes, `camelCase` for functions/variables
            - Prefer data classes for DTOs; use sealed classes for exhaustive state
            - Null safety: never use `!!` operator; use `?.let`, `?: return`, or `require` guards
            - Extension functions for utility logic; avoid static utility classes

            ## Preferred Patterns
            - Kotlin Coroutines + Flow for async; `suspend` functions over callbacks
            - Ktor or Spring Boot for web services; dependency injection via Koin or Spring
            - `Result<T>` or Arrow's `Either<L, R>` for functional error handling
            - Immutable collections by default (`listOf`, `mapOf`); `mutableListOf` only when mutation is needed
            - Property delegation (`by lazy`, `by viewModels`) for initialization patterns

            ## Dependency Standards
            - Build: Gradle Kotlin DSL with version catalog (`libs.versions.toml`)
            - Testing: Kotest or JUnit 5 + MockK
            - Serialization: `kotlinx.serialization`
            - HTTP client: Ktor client or OkHttp + Retrofit

            ## Generation Hints
            - Use `src/main/kotlin` and `src/test/kotlin` directory structure
            - One class per file, file name matches class name
            - Add `kotlin.code.style=official` to `gradle.properties`
            - Include `detekt` for static analysis
            - Target JVM 21 (`jvmToolchain(21)` in Gradle)
            """),

        new("ruby",
            "Ruby Guidelines",
            "Conventions and patterns for Ruby projects.",
            """
            ## Conventions
            - Ruby 3.2+ minimum; enable frozen string literals (`# frozen_string_literal: true`)
            - RuboCop with standard config for style enforcement
            - `snake_case` everywhere; `PascalCase` for classes/modules; `SCREAMING_SNAKE_CASE` for constants
            - Two-space indentation; no trailing whitespace
            - Prefer `&&`/`||` over `and`/`or` in conditional expressions

            ## Preferred Patterns
            - Rails conventions for web apps (REST resourceful routing, fat model / thin controller)
            - Service objects for complex business logic outside of models
            - `Struct` or `Data.define` (Ruby 3.2+) for simple value objects
            - Keyword arguments for methods with 3+ parameters
            - `Enumerable` methods over manual iteration (`map`, `select`, `reduce`)

            ## Dependency Standards
            - Bundle with `Gemfile` + `Gemfile.lock`; specify exact Ruby version in `.ruby-version`
            - Testing: RSpec + FactoryBot + Shoulda Matchers
            - HTTP: `faraday` for HTTP clients; `rack` for low-level middleware
            - Background jobs: Sidekiq (Redis-backed) or GoodJob (Postgres-backed)
            - Linting: RuboCop + rubocop-rails (for Rails projects)

            ## Generation Hints
            - Use `bin/setup` script for first-time project setup
            - Add `Procfile` for process definitions (web, worker)
            - Scaffold `spec/` with `spec_helper.rb` and `rails_helper.rb`
            - Include `config/database.yml.example` (committed) + `config/database.yml` (.gitignored)
            - Add `brakeman` for security scanning and `bundler-audit` for dependency CVE checks
            """),

        new("swift",
            "Swift Guidelines",
            "Conventions and patterns for Swift projects.",
            """
            ## Conventions
            - Swift 5.9+ minimum; use Swift 6 strict concurrency (`-strict-concurrency=complete`) for new projects
            - SwiftLint for style enforcement
            - `camelCase` for variables/functions, `PascalCase` for types; avoid abbreviations
            - Prefer `struct` over `class` for value semantics; use `class` only when identity or reference semantics are required
            - Mark types `final` unless designed for subclassing

            ## Preferred Patterns
            - Swift Concurrency (`async/await`, `Actor`) over GCD and callbacks
            - `Result<T, Error>` or `throws` for error propagation
            - Protocol-oriented design; prefer small focused protocols
            - `@Observable` macro (Swift 5.9+) for SwiftUI state; avoid `@StateObject` for new code
            - Dependency injection via initializer parameters; avoid singletons for testable code

            ## Dependency Standards
            - Swift Package Manager (SPM) exclusively for new projects; avoid CocoaPods
            - Networking: `URLSession` with async APIs; `Alamofire` only for legacy codebases
            - Testing: XCTest + Swift Testing framework (Swift 5.9+)
            - JSON: `Codable` (built-in); avoid third-party JSON libraries

            ## Generation Hints
            - Organize as `Sources/<Target>` + `Tests/<Target>Tests` per SPM conventions
            - Include `Package.swift` with explicit platform/Swift version constraints
            - Add `.swiftlint.yml` with standard rule set
            - Use `#Preview` macros for SwiftUI previews (Xcode 15+)
            - Add a `Makefile` with `build`, `test`, `lint` targets for CI
            """),

        new("lua",
            "Lua Guidelines",
            "Conventions and patterns for Lua projects.",
            """
            ## Conventions
            - Lua 5.4 minimum (or LuaJIT 2.1 for performance-critical work)
            - `snake_case` for variables/functions, `PascalCase` for module tables and classes
            - Local variables always (`local x = ...`); global variables are bugs, not features
            - Two-space indentation; lines ≤ 100 characters
            - Module pattern: return a table from each module file

            ## Preferred Patterns
            - OOP via metatables and `__index`; use a base `Class` library or implement consistently
            - Error handling with `pcall`/`xpcall` at boundaries; return `nil, err` from fallible functions
            - Coroutines for cooperative multitasking; `vim.loop` / `libuv` for async I/O (Neovim plugins)
            - `require` for imports; structure `src/` as a package with `init.lua` entry points
            - String manipulation with `string.format`, `gmatch`, `gsub` over concatenation loops

            ## Dependency Standards
            - Package management: LuaRocks with a `<project>.rockspec`
            - Testing: Busted test framework
            - JSON: `lua-cjson` or `dkjson` for JSON parsing
            - Web: OpenResty/nginx + lua-resty-* stack for web services

            ## Generation Hints
            - Add a `Makefile` with `test`, `lint` (luacheck), and `install` targets
            - Include `.luacheckrc` for luacheck static analysis configuration
            - Document public API with LDoc-compatible `---` comments
            - Include a `spec/` directory with Busted test files
            """),

        new("zig",
            "Zig Guidelines",
            "Conventions and patterns for Zig projects.",
            """
            ## Conventions
            - Zig 0.13+ (track stable channel); update toolchain via `zigup`
            - `snake_case` for variables/functions, `PascalCase` for types/structs/enums
            - No implicit allocations: every function that allocates takes an `Allocator` parameter
            - Prefer stack allocation; use `ArrayList`, `HashMap` from `std.ArrayList` for heap collections
            - `comptime` for generic programming and compile-time validation

            ## Preferred Patterns
            - Explicit error handling with error unions (`!T`); no exceptions, no panics in library code
            - `defer` for cleanup; never skip resource release
            - Tagged unions for sum types; `switch` must be exhaustive
            - `std.testing` for unit tests; place `test` blocks in the same file as the code under test
            - Pass `std.io.Writer`/`std.io.Reader` interfaces instead of concrete types for I/O

            ## Dependency Standards
            - Zig Build System (`build.zig`) exclusively; no external build tools
            - Package management: `zig fetch` + `build.zig.zon` for dependencies
            - Cross-compile via `zig build -Dtarget=<triple>`
            - Testing: built-in `zig test`

            ## Generation Hints
            - Place library code in `src/root.zig` (exported from `build.zig`)
            - Executables use `src/main.zig`; library + executable in same project is common
            - Add `build.zig.zon` with `name`, `version`, and `paths` fields
            - Use `std.log` (not `std.debug.print`) for application logging
            - Document public declarations with `///` doc comments
            """),

        new("cpp",
            "C++ / CMake Guidelines",
            "Conventions and patterns for modern C++ projects.",
            """
            ## Conventions
            - C++20 minimum (`set(CMAKE_CXX_STANDARD 20)` in CMake)
            - `snake_case` for variables/functions, `PascalCase` for classes, `kConstantCase` for constants
            - RAII everywhere: resources are owned by objects, never raw `new`/`delete`
            - `const`-correctness: mark all member functions and parameters `const` where possible
            - No raw pointers in new code; use `std::unique_ptr`, `std::shared_ptr`, or references

            ## Preferred Patterns
            - Smart pointers: `unique_ptr` by default; `shared_ptr` only when shared ownership is required
            - `std::optional<T>` for optional return values; `std::expected<T, E>` (C++23) for error propagation
            - Range-based for loops and standard algorithms over manual iteration
            - Structured bindings (`auto [key, val] = ...`) for pair/tuple unpacking
            - Move semantics: implement move constructor/assignment for resource-owning types

            ## Dependency Standards
            - Build: CMake 3.25+ with `FetchContent` or vcpkg for dependencies
            - Testing: Google Test (gtest) + Google Mock; Catch2 is also acceptable
            - Formatting: `clang-format` with project `.clang-format` config
            - Static analysis: `clang-tidy` with `.clang-tidy` config; enable `cppcoreguidelines-*` checks
            - Sanitizers: enable ASAN/UBSAN in Debug builds (`-fsanitize=address,undefined`)

            ## Generation Hints
            - Organize as `include/<project>/` (public headers) + `src/` (implementation)
            - Use CMake presets (`CMakePresets.json`) for reproducible debug/release/CI configurations
            - Add `compile_commands.json` generation (`set(CMAKE_EXPORT_COMPILE_COMMANDS ON)`)
            - Include a `conanfile.txt` or `vcpkg.json` for package management
            - Add `Dockerfile` for hermetic builds if targeting Linux deployment
            """),
    };

    public static BuiltInGuideline? Get(string slug) =>
        All.FirstOrDefault(g => string.Equals(g.Slug, slug, StringComparison.OrdinalIgnoreCase));

    /// <summary>Reconstruct a full editor-ready markdown string from a built-in guideline.</summary>
    public static string ToMarkdown(BuiltInGuideline g)
    {
        ArgumentNullException.ThrowIfNull(g);
        return $"---\nname: {g.Name}\ndescription: {g.Description}\n---\n\n{g.Body.TrimStart()}";
    }
}
