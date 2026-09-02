using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Minimal;

/// <summary>Phase 73 — Lua script scaffold (LuaRocks, busted tests).</summary>
public sealed class LuaScriptScaffold : IProjectTemplate
{
    public string Id => "lua/script";
    public string Language => "lua";
    public string Kind => "script";
    public string Name => "Lua Script";
    public string Description => "Lua script with a rockspec and busted unit tests.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var snake = ScaffoldHelpers.ToSnakeCase(context.ProjectName);
        var kebab = ScaffoldHelpers.ToKebabCase(context.ProjectName);

        return
        [
            new($"{kebab}-0.1.0-1.rockspec", $$"""
                package = "{{kebab}}"
                version = "0.1.0-1"

                source = {
                  url = "*** placeholder ***",
                }

                description = {
                  summary = "{{kebab}}",
                  license = "MIT",
                }

                dependencies = {
                  "lua >= 5.4",
                }

                build = {
                  type = "builtin",
                  modules = {
                    ["{{snake}}"] = "src/{{snake}}.lua",
                  },
                }
                """),

            new($"src/{snake}.lua", $$"""
                local M = {}

                --- Returns a greeting for the given name.
                ---@param name string
                ---@return string
                function M.greet(name)
                  return "Hello, " .. (name or "World") .. "!"
                end

                return M
                """),

            new("bin/main.lua", $$"""
                #!/usr/bin/env lua
                package.path = package.path .. ";./src/?.lua"
                local m = require("{{snake}}")
                local name = arg[1] or "World"
                print(m.greet(name))
                """, IsExecutable: true),

            new($"spec/{snake}_spec.lua", $$"""
                local {{snake}} = require("{{snake}}")

                describe("greet", function()
                  it("returns a salutation", function()
                    assert.are.equal("Hello, Sovrant!", {{snake}}.greet("Sovrant"))
                  end)

                  it("includes the name", function()
                    assert.is_truthy({{snake}}.greet("World"):find("World"))
                  end)
                end)
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
                      - run: sudo apt-get install -y lua5.4 luarocks
                      - run: sudo luarocks install busted
                      - run: busted spec/
                """),

            new(".gitignore", """
                *.rock
                luarocks/
                .luarocks/
                .env
                """),

            new("README.md", $$"""
                # {{kebab}}

                A Lua script.

                ## Run

                ```bash
                lua bin/main.lua World
                ```

                ## Test

                ```bash
                # Install busted: luarocks install busted
                busted spec/
                ```
                """),
        ];
    }
}
