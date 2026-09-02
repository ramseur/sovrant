using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Minimal;

/// <summary>Phase 73 — Ruby script scaffold (Bundler, Minitest).</summary>
public sealed class RubyScriptScaffold : IProjectTemplate
{
    public string Id => "ruby/script";
    public string Language => "ruby";
    public string Kind => "script";
    public string Name => "Ruby Script";
    public string Description => "Ruby script with Bundler gemfile and Minitest tests.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var snake = ScaffoldHelpers.ToSnakeCase(context.ProjectName);
        var kebab = ScaffoldHelpers.ToKebabCase(context.ProjectName);

        return
        [
            new("Gemfile", $$"""
                # frozen_string_literal: true

                source "https://rubygems.org"

                gem "minitest", "~> 5.25"
                """),

            new($"lib/{snake}.rb", $$"""
                # frozen_string_literal: true

                module {{ScaffoldHelpers.ToPascalCase(context.ProjectName)}}
                  def self.greet(name)
                    "Hello, #{name}!"
                  end
                end
                """),

            new($"bin/{kebab}", $$"""
                #!/usr/bin/env ruby
                # frozen_string_literal: true

                $LOAD_PATH.unshift(File.join(__dir__, "..", "lib"))
                require "{{snake}}"

                name = ARGV[0] || "World"
                puts {{ScaffoldHelpers.ToPascalCase(context.ProjectName)}}.greet(name)
                """, IsExecutable: true),

            new($"test/test_{snake}.rb", $$"""
                # frozen_string_literal: true

                require "minitest/autorun"
                require_relative "../lib/{{snake}}"

                class Test{{ScaffoldHelpers.ToPascalCase(context.ProjectName)}} < Minitest::Test
                  def test_greet_returns_salutation
                    assert_equal "Hello, Sovrant!", {{ScaffoldHelpers.ToPascalCase(context.ProjectName)}}.greet("Sovrant")
                  end

                  def test_greet_includes_name
                    assert_includes {{ScaffoldHelpers.ToPascalCase(context.ProjectName)}}.greet("World"), "World"
                  end
                end
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
                      - uses: ruby/setup-ruby@v1
                        with:
                          bundler-cache: true
                      - run: bundle exec ruby -Itest test/test_*.rb
                """),

            new(".gitignore", """
                .bundle/
                vendor/
                *.gem
                .env
                """),

            new("README.md", $$"""
                # {{kebab}}

                A Ruby script.

                ## Setup

                ```bash
                bundle install
                ```

                ## Run

                ```bash
                ruby bin/{{kebab}} World
                ```

                ## Test

                ```bash
                bundle exec ruby -Itest test/test_{{snake}}.rb
                ```
                """),
        ];
    }
}
