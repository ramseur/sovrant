using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Minimal;

/// <summary>Phase 73 — Swift CLI scaffold (Swift Package Manager, XCTest).</summary>
public sealed class SwiftCliScaffold : IProjectTemplate
{
    public string Id => "swift/cli";
    public string Language => "swift";
    public string Kind => "cli";
    public string Name => "Swift CLI";
    public string Description => "Swift command-line tool using Swift Package Manager with XCTest unit tests.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pascal = ScaffoldHelpers.ToPascalCase(context.ProjectName);

        return
        [
            new("Package.swift", $$"""
                // swift-tools-version: 5.10
                import PackageDescription

                let package = Package(
                    name: "{{pascal}}",
                    targets: [
                        .executableTarget(name: "{{pascal}}", path: "Sources/{{pascal}}"),
                        .testTarget(
                            name: "{{pascal}}Tests",
                            dependencies: [],
                            path: "Tests/{{pascal}}Tests"
                        ),
                    ]
                )
                """),

            new($"Sources/{pascal}/main.swift", $$"""
                import Foundation

                func greet(_ name: String) -> String {
                    "Hello, \\(name)!"
                }

                let name = CommandLine.arguments.dropFirst().first ?? "World"
                print(greet(name))
                """),

            new($"Tests/{pascal}Tests/{pascal}Tests.swift", $$"""
                import XCTest

                final class {{pascal}}Tests: XCTestCase {
                    func testGreetReturnsSalutation() {
                        XCTAssertEqual(greet("Sovrant"), "Hello, Sovrant!")
                    }

                    func testGreetIncludesName() {
                        XCTAssertTrue(greet("World").contains("World"))
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
                    runs-on: macos-latest
                    steps:
                      - uses: actions/checkout@v4
                      - run: swift build
                      - run: swift test
                """),

            new(".gitignore", """
                .build/
                .swiftpm/
                *.xcodeproj
                .env
                """),

            new("README.md", $$"""
                # {{pascal}}

                A Swift CLI tool.

                ## Run

                ```bash
                swift run {{pascal}} World
                ```

                ## Test

                ```bash
                swift test
                ```

                ## Build (release)

                ```bash
                swift build -c release
                .build/release/{{pascal}} World
                ```
                """),
        ];
    }
}
