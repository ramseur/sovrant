using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.DotNet;

/// <summary>Phase 73 — .NET 10 minimal-API Web API scaffold with xUnit + WebApplicationFactory.</summary>
public sealed class DotNetWebApiScaffold : IProjectTemplate
{
    public string Id => "dotnet/webapi";
    public string Language => "dotnet";
    public string Kind => "webapi";
    public string Name => ".NET Web API";
    public string Description => ".NET 10 minimal API with health endpoint, OpenAPI, and an xUnit integration test project.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pascal = ScaffoldHelpers.ToPascalCase(context.ProjectName);

        return
        [
            new($"{pascal}/{pascal}.csproj", $$"""
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <AnalysisMode>All</AnalysisMode>
                  </PropertyGroup>
                </Project>
                """),

            new($"{pascal}/Program.cs", """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddOpenApi();

                var app = builder.Build();

                if (app.Environment.IsDevelopment())
                    app.MapOpenApi();

                app.MapGet("/health", () => new { status = "ok" });
                app.MapGet("/", () => "Hello World!");

                app.Run();

                public partial class Program { }
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
                      "Default": "Debug",
                      "Microsoft.AspNetCore": "Information"
                    }
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
                    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
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

            new($"{pascal}.Tests/HealthTests.cs", $$"""
                using Microsoft.AspNetCore.Mvc.Testing;
                using System.Net;
                using System.Net.Http.Json;

                namespace {{pascal}}.Tests;

                public sealed class HealthTests(WebApplicationFactory<Program> factory)
                    : IClassFixture<WebApplicationFactory<Program>>
                {
                    [Fact]
                    public async Task HealthEndpoint_Returns200()
                    {
                        var client = factory.CreateClient();
                        var response = await client.GetAsync("/health");
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    }

                    [Fact]
                    public async Task HealthEndpoint_ReturnsOkStatus()
                    {
                        var client = factory.CreateClient();
                        var body = await client.GetFromJsonAsync<HealthResponse>("/health");
                        Assert.NotNull(body);
                        Assert.Equal("ok", body.Status);
                    }

                    private sealed record HealthResponse(string Status);
                }
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

                A .NET 10 minimal API.

                ## Run

                ```bash
                dotnet run --project {{pascal}}
                # Open http://localhost:5000/health
                ```

                ## Test

                ```bash
                dotnet test
                ```

                ## Endpoints

                | Method | Path | Description |
                |--------|------|-------------|
                | GET | /health | Returns `{"status":"ok"}` |
                | GET | / | Hello World |
                | GET | /openapi/v1.json | OpenAPI spec (dev only) |
                """),
        ];
    }
}
