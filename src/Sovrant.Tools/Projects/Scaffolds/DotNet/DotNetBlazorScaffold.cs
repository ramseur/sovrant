using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.DotNet;

/// <summary>Phase 73 — Blazor Server scaffold (.NET 10, xUnit integration tests).</summary>
public sealed class DotNetBlazorScaffold : IProjectTemplate
{
    public string Id => "dotnet/blazor";
    public string Language => "dotnet";
    public string Kind => "blazor";
    public string Name => "Blazor Server App";
    public string Description => "Blazor Server application targeting .NET 10 with xUnit integration tests.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pascal = ScaffoldHelpers.ToPascalCase(context.ProjectName);
        var kebab = ScaffoldHelpers.ToKebabCase(context.ProjectName);
        var mainGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
        var testGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();

        return
        [
            new($"{pascal}/{pascal}.csproj", $$"""
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <RootNamespace>{{pascal}}</RootNamespace>
                  </PropertyGroup>
                </Project>
                """),

            new($"{pascal}/Program.cs", $$"""
                using {{pascal}}.Components;

                var builder = WebApplication.CreateBuilder(args);

                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents();

                var app = builder.Build();

                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/Error");
                    app.UseHsts();
                }

                app.UseHttpsRedirection();
                app.UseAntiforgery();

                app.MapStaticAssets();
                app.MapRazorComponents<App>()
                    .AddInteractiveServerRenderMode();

                app.Run();

                public partial class Program { }
                """),

            new($"{pascal}/Components/App.razor", $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <title>{{pascal}}</title>
                    <base href="/" />
                    <link rel="stylesheet" href="app.css" />
                    <HeadOutlet />
                </head>
                <body>
                    <Routes />
                    <script src="_framework/blazor.web.js"></script>
                </body>
                </html>
                """),

            new($"{pascal}/Components/Routes.razor", """
                <Router AppAssembly="typeof(App).Assembly">
                    <Found Context="routeData">
                        <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)" />
                        <FocusOnNavigate RouteData="routeData" Selector="h1" />
                    </Found>
                </Router>
                """),

            new($"{pascal}/Components/Layout/MainLayout.razor", """
                @inherits LayoutComponentBase

                <div class="page">
                    <main>
                        <article class="content">
                            @Body
                        </article>
                    </main>
                </div>
                """),

            new($"{pascal}/Components/Pages/Home.razor", $$"""
                @page "/"
                @rendermode InteractiveServer

                <PageTitle>Home</PageTitle>

                <h1>Hello, @_name!</h1>

                <div>
                    <input @bind="_name" placeholder="Enter your name" />
                </div>

                @code {
                    private string _name = "World";
                }
                """),

            new($"{pascal}/wwwroot/app.css", """
                body {
                    font-family: system-ui, sans-serif;
                    margin: 2rem;
                }
                """),

            new($"{pascal}/appsettings.json", """
                {
                  "Logging": {
                    "LogLevel": {
                      "Default": "Information",
                      "Microsoft.AspNetCore": "Warning"
                    }
                  },
                  "AllowedHosts": "*"
                }
                """),

            new($"{pascal}/appsettings.Development.json", """
                {
                  "Logging": {
                    "LogLevel": {
                      "Default": "Information",
                      "Microsoft.AspNetCore": "Warning"
                    }
                  }
                }
                """),

            new($"{pascal}.Tests/{pascal}.Tests.csproj", $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <IsPackable>false</IsPackable>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.*" />
                    <PackageReference Include="xunit" Version="2.9.*" />
                    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.*">
                      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
                      <PrivateAssets>all</PrivateAssets>
                    </PackageReference>
                  </ItemGroup>
                  <ItemGroup>
                    <ProjectReference Include="../{{pascal}}/{{pascal}}.csproj" />
                  </ItemGroup>
                </Project>
                """),

            new($"{pascal}.Tests/HomePageTests.cs", $$"""
                using Microsoft.AspNetCore.Mvc.Testing;
                using System.Net;

                namespace {{pascal}}.Tests;

                public class HomePageTests(WebApplicationFactory<Program> factory)
                    : IClassFixture<WebApplicationFactory<Program>>
                {
                    [Fact]
                    public async Task HomePage_ReturnsOk()
                    {
                        var client = factory.CreateClient();
                        var response = await client.GetAsync("/");
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    }

                    [Fact]
                    public async Task HomePage_ContainsHello()
                    {
                        var client = factory.CreateClient();
                        var html = await client.GetStringAsync("/");
                        Assert.Contains("Hello", html, StringComparison.OrdinalIgnoreCase);
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

                [*.{cs,csproj,props,targets,razor}]
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
                .vs/
                *.user
                .env
                """),

            new("README.md", $$"""
                # {{pascal}}

                A Blazor Server application.

                ## Run

                ```bash
                dotnet run --project {{pascal}}
                ```

                ## Test

                ```bash
                dotnet test
                ```

                ## Build (release)

                ```bash
                dotnet build -c Release
                ```
                """),
        ];
    }
}
