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
