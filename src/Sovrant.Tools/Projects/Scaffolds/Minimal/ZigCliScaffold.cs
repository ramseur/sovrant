using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Minimal;

/// <summary>Phase 73 — Zig CLI scaffold (build.zig, built-in testing).</summary>
public sealed class ZigCliScaffold : IProjectTemplate
{
    public string Id => "zig/cli";
    public string Language => "zig";
    public string Kind => "cli";
    public string Name => "Zig CLI";
    public string Description => "Zig command-line executable with build.zig and built-in test runner.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var snake = ScaffoldHelpers.ToSnakeCase(context.ProjectName);
        var kebab = ScaffoldHelpers.ToKebabCase(context.ProjectName);

        return
        [
            new("build.zig", $$"""
                const std = @import("std");

                pub fn build(b: *std.Build) void {
                    const target = b.standardTargetOptions(.{});
                    const optimize = b.standardOptimizeOption(.{});

                    const exe = b.addExecutable(.{
                        .name = "{{kebab}}",
                        .root_source_file = b.path("src/main.zig"),
                        .target = target,
                        .optimize = optimize,
                    });
                    b.installArtifact(exe);

                    const run_cmd = b.addRunArtifact(exe);
                    run_cmd.step.dependOn(b.getInstallStep());
                    if (b.args) |args| run_cmd.addArgs(args);
                    const run_step = b.step("run", "Run the app");
                    run_step.dependOn(&run_cmd.step);

                    const lib_unit_tests = b.addTest(.{
                        .root_source_file = b.path("src/lib.zig"),
                        .target = target,
                        .optimize = optimize,
                    });
                    const run_lib_unit_tests = b.addRunArtifact(lib_unit_tests);
                    const test_step = b.step("test", "Run unit tests");
                    test_step.dependOn(&run_lib_unit_tests.step);
                }
                """),

            new("build.zig.zon", $$"""
                .{
                    .name = .{{snake}},
                    .version = "0.1.0",
                    .minimum_zig_version = "0.14.0",
                    .dependencies = .{},
                    .paths = .{"."},
                }
                """),

            new("src/lib.zig", $$"""
                const std = @import("std");

                /// Returns a greeting for the given name.
                pub fn greet(allocator: std.mem.Allocator, name: []const u8) ![]u8 {
                    return std.fmt.allocPrint(allocator, "Hello, {s}!", .{name});
                }

                test "greet returns salutation" {
                    const result = try greet(std.testing.allocator, "Sovrant");
                    defer std.testing.allocator.free(result);
                    try std.testing.expectEqualStrings("Hello, Sovrant!", result);
                }

                test "greet includes name" {
                    const result = try greet(std.testing.allocator, "World");
                    defer std.testing.allocator.free(result);
                    try std.testing.expect(std.mem.indexOf(u8, result, "World") != null);
                }
                """),

            new("src/main.zig", """
                const std = @import("std");
                const lib = @import("lib.zig");

                pub fn main() !void {
                    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
                    defer _ = gpa.deinit();
                    const allocator = gpa.allocator();

                    const args = try std.process.argsAlloc(allocator);
                    defer std.process.argsFree(allocator, args);

                    const name: []const u8 = if (args.len > 1) args[1] else "World";
                    const msg = try lib.greet(allocator, name);
                    defer allocator.free(msg);

                    const stdout = std.io.getStdOut().writer();
                    try stdout.print("{s}\n", .{msg});
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
                      - uses: mlugg/setup-zig@v1
                        with:
                          version: '0.14.0'
                      - run: zig build
                      - run: zig build test
                """),

            new(".gitignore", """
                zig-out/
                zig-cache/
                .zig-cache/
                .env
                """),

            new("README.md", $$"""
                # {{kebab}}

                A Zig CLI application.

                ## Run

                ```bash
                zig build run -- World
                ```

                ## Test

                ```bash
                zig build test
                ```

                ## Build (release)

                ```bash
                zig build -Doptimize=ReleaseSafe
                ./zig-out/bin/{{kebab}} World
                ```
                """),
        ];
    }
}
