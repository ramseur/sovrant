using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.DotNet;

/// <summary>Phase 73 — .NET 10 console app scaffold with xUnit test project.</summary>
public sealed class DotNetConsoleScaffold : IProjectTemplate
{
    public string Id => "dotnet/console";
    public string Language => "dotnet";
    public string Kind => "console";
    public string Name => ".NET Console App";
    public string Description => ".NET 10 console application with top-level statements, nullable enabled, and an xUnit test project.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pascal = ScaffoldHelpers.ToPascalCase(context.ProjectName);
        var mainGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
        var testGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();

        return
        [
            new($"{pascal}/{pascal}.csproj", $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <AnalysisMode>All</AnalysisMode>
                  </PropertyGroup>
                </Project>
                """),

            new($"{pascal}/Program.cs", $$"""
                var name = args.Length > 0 ? args[0] : "World";
                Console.WriteLine(Greeter.Greet(name));

                public static class Greeter
                {
                    public static string Greet(string name) => $"Hello, {name}!";
                }
                """),

            new($"{pascal}.Tests/{pascal}.Tests.csproj", $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <IsPackable>false</IsPackable>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
                    <PackageReference Include="xunit" Version="2.9.*" />
                    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.*">
                      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
                      <PrivateAssets>all</PrivateAssets>
                    </PackageReference>
                  </ItemGroup>
                  <ItemGroup>
                    <ProjectReference Include="../{{pascal}}/{{pascal}}.csproj" />
                  </ItemGroup>
                </Project>
                """),

            new($"{pascal}.Tests/GreeterTests.cs", $$"""
                namespace {{pascal}}.Tests;

                public sealed class GreeterTests
                {
                    [Fact]
                    public void Greet_ReturnsSalutation()
                    {
                        var result = Greeter.Greet("Sovrant");
                        Assert.Equal("Hello, Sovrant!", result);
                    }

                    [Theory]
                    [InlineData("Alice")]
                    [InlineData("Bob")]
                    public void Greet_IncludesName(string name)
                    {
                        var result = Greeter.Greet(name);
                        Assert.Contains(name, result);
                    }
                }
                """),

            new($"{pascal}.sln", $$"""
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                VisualStudioVersion = 17.0.31903.59
                MinimumVisualStudioVersion = 10.0.40219.1
                Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{pascal}}", "{{pascal}}/{{pascal}}.csproj", "{{mainGuid}}"
                EndProject
                Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{pascal}}.Tests", "{{pascal}}.Tests/{{pascal}}.Tests.csproj", "{{testGuid}}"
                EndProject
                Global
                	GlobalSection(SolutionConfigurationPlatforms) = preSolution
                		Debug|Any CPU = Debug|Any CPU
                		Release|Any CPU = Release|Any CPU
                	EndGlobalSection
                	GlobalSection(ProjectConfigurationPlatforms) = postSolution
                		{{mainGuid}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                		{{mainGuid}}.Debug|Any CPU.Build.0 = Debug|Any CPU
                		{{mainGuid}}.Release|Any CPU.ActiveCfg = Release|Any CPU
                		{{mainGuid}}.Release|Any CPU.Build.0 = Release|Any CPU
                		{{testGuid}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                		{{testGuid}}.Debug|Any CPU.Build.0 = Debug|Any CPU
                		{{testGuid}}.Release|Any CPU.ActiveCfg = Release|Any CPU
                		{{testGuid}}.Release|Any CPU.Build.0 = Release|Any CPU
                	EndGlobalSection
                	GlobalSection(SolutionProperties) = preSolution
                		HideSolutionNode = FALSE
                	EndGlobalSection
                EndGlobal
                """),

            new("Directory.Build.props", """
                <Project>
                  <PropertyGroup>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <AnalysisMode>All</AnalysisMode>
                    <LangVersion>latest</LangVersion>
                  </PropertyGroup>
                </Project>
                """),

            new(".editorconfig", """
                root = true

                [*]
                indent_style = space
                end_of_line = lf
                charset = utf-8
                trim_trailing_whitespace = true
                insert_final_newline = true

                [*.{cs,csproj,props,targets}]
                indent_size = 4

                [*.{json,yaml,yml}]
                indent_size = 2

                [*.md]
                trim_trailing_whitespace = false
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
                      - uses: actions/setup-dotnet@v4
                        with:
                          dotnet-version: '10.x'
                      - run: dotnet restore
                      - run: dotnet build --no-restore
                      - run: dotnet test --no-build
                """),

            new(".gitignore", """
                bin/
                obj/
                *.user
                .vs/
                .vscode/
                *.suo
                """),

            new("README.md", $$"""
                # {{pascal}}

                A .NET 10 console application.

                ## Run

                ```bash
                dotnet run --project {{pascal}} -- World
                ```

                ## Test

                ```bash
                dotnet test
                ```

                ## Build

                ```bash
                dotnet build
                ```
                """),
        ];
    }
}
