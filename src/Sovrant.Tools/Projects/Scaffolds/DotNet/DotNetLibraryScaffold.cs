using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.DotNet;

/// <summary>Phase 73 — .NET 10 class library scaffold with NuGet packaging and xUnit tests.</summary>
public sealed class DotNetLibraryScaffold : IProjectTemplate
{
    public string Id => "dotnet/library";
    public string Language => "dotnet";
    public string Kind => "library";
    public string Name => ".NET Class Library";
    public string Description => ".NET 10 class library ready for NuGet packaging, with nullable, analyzers, and xUnit test project.";
    public IReadOnlyList<ScaffoldParameter> Parameters =>
    [
        new("Author", "Package author name", Required: false, Default: ""),
        new("Description", "Short package description", Required: false, Default: ""),
    ];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pascal = ScaffoldHelpers.ToPascalCase(context.ProjectName);
        var author = context.Get("Author", "Your Name");
        var description = context.Get("Description", $"A .NET library: {pascal}");
        var mainGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
        var testGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();

        return
        [
            new($"{pascal}/{pascal}.csproj", $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <AnalysisMode>All</AnalysisMode>
                    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
                    <PackageId>{{pascal}}</PackageId>
                    <Version>0.1.0</Version>
                    <Authors>{{author}}</Authors>
                    <Description>{{description}}</Description>
                    <PackageLicenseExpression>MIT</PackageLicenseExpression>
                  </PropertyGroup>
                </Project>
                """),

            new($"{pascal}/{pascal}.cs", $$"""
                namespace {{pascal}};

                /// <summary>Entry point for the {{pascal}} library.</summary>
                public static class {{pascal}}Client
                {
                    /// <summary>Returns a greeting message.</summary>
                    public static string Greet(string name)
                    {
                        ArgumentNullException.ThrowIfNull(name);
                        return $"Hello from {{pascal}}, {name}!";
                    }
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

            new($"{pascal}.Tests/{pascal}ClientTests.cs", $$"""
                using {{pascal}};

                namespace {{pascal}}.Tests;

                public sealed class {{pascal}}ClientTests
                {
                    [Fact]
                    public void Greet_ContainsName()
                    {
                        var result = {{pascal}}Client.Greet("Sovrant");
                        Assert.Contains("Sovrant", result);
                    }

                    [Fact]
                    public void Greet_NullName_Throws()
                    {
                        Assert.Throws<ArgumentNullException>(() => {{pascal}}Client.Greet(null!));
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
                *.nupkg
                """),

            new("README.md", $$"""
                # {{pascal}}

                {{description}}

                ## Install

                ```bash
                dotnet add package {{pascal}}
                ```

                ## Usage

                ```csharp
                using {{pascal}};

                Console.WriteLine({{pascal}}Client.Greet("World"));
                ```

                ## Development

                ```bash
                dotnet test
                dotnet pack {{pascal}}
                ```
                """),
        ];
    }
}
