#pragma warning disable CA1308 // ToLowerInvariant on ASCII language IDs is intentional and locale-safe
namespace Sovrant.Tools.Projects.Scaffolds;

/// <summary>
/// Derives the canonical build/run/test shell commands and entry point for a
/// scaffolded project based on its language and kind.
/// These are temporary defaults — Phase 128 Part D will promote them to
/// <c>IProjectTemplate</c> members so each template can override them precisely.
/// </summary>
internal static class ScaffoldCommands
{
    /// <summary>
    /// Returns <c>(buildCommand, runCommand, testCommand)</c> for the given language and kind.
    /// <paramref name="projectName"/> is used where the command must reference a specific file.
    /// Null means "not applicable for this language/kind".
    /// </summary>
    public static (string? build, string? run, string? test) For(string language, string kind, string projectName) =>
        language.ToLowerInvariant() switch
        {
            "dotnet" => DotNet(kind, projectName),
            "node"   => Node(kind),
            "python" => Python(kind),
            "go"     => ("go build ./...", "go run .", "go test ./..."),
            "rust"   => ("cargo build", "cargo run", "cargo test"),
            "java"   => ("mvn package -q", $"java -jar target/{projectName}-1.0-SNAPSHOT.jar", "mvn test -q"),
            "kotlin" => ("./gradlew build", "./gradlew run", "./gradlew test"),
            "ruby"   => ("bundle install", "bundle exec ruby main.rb", "bundle exec rspec"),
            "swift"  => ("swift build", $".build/debug/{projectName}", "swift test"),
            "lua"    => (null, "lua main.lua", null),
            "zig"    => ("zig build", $"zig-out/bin/{projectName}", "zig build test"),
            "cpp"    => ("cmake -B build && cmake --build build", $"./build/{projectName}", "ctest --test-dir build"),
            _        => (null, null, null),
        };

    /// <summary>
    /// Returns the primary entry point path relative to the artifact run root
    /// (i.e. including the project name directory prefix).
    /// </summary>
    public static string? EntryPoint(string language, string projectName) =>
        language.ToLowerInvariant() switch
        {
            "dotnet" => $"{projectName}/{projectName}.csproj",
            "node"   => $"{projectName}/package.json",
            "python" => $"{projectName}/main.py",
            "go"     => $"{projectName}/main.go",
            "rust"   => $"{projectName}/Cargo.toml",
            "java"   => $"{projectName}/pom.xml",
            "kotlin" => $"{projectName}/build.gradle.kts",
            "ruby"   => $"{projectName}/Gemfile",
            "swift"  => $"{projectName}/Package.swift",
            "lua"    => $"{projectName}/main.lua",
            "zig"    => $"{projectName}/build.zig",
            "cpp"    => $"{projectName}/CMakeLists.txt",
            _        => null,
        };

    // ── Per-language helpers ────────────────────────────────────────────

    private static (string? build, string? run, string? test) DotNet(string kind, string projectName) =>
        kind switch
        {
            "library" => ("dotnet build", null, "dotnet test"),
            "blazor"  => ("dotnet build", $"dotnet run --project {projectName}/{projectName}.csproj", null),
            "worker"  => ("dotnet build", $"dotnet run --project {projectName}/{projectName}.csproj", null),
            // webapi, console, and everything else
            _         => ("dotnet build", $"dotnet run --project {projectName}/{projectName}.csproj", "dotnet test"),
        };

    private static (string? build, string? run, string? test) Node(string kind) =>
        kind switch
        {
            "nextjs"    => ("npm install && npm run build", "npm run dev", "npm test"),
            "monorepo"  => ("npm install", "npm run dev --workspaces", "npm test --workspaces"),
            "library"   => ("npm install && npm run build", null, "npm test"),
            // cli, express-api, and everything else
            _           => ("npm install", "npm start", "npm test"),
        };

    private static (string? build, string? run, string? test) Python(string kind) =>
        kind switch
        {
            "fastapi" => ("pip install -r requirements.txt", "uvicorn main:app --reload", "pytest"),
            _         => ("pip install -r requirements.txt", "python main.py", "pytest"),
        };
}
