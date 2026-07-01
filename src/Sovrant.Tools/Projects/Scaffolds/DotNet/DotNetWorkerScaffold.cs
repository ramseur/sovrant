using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.DotNet;

/// <summary>Phase 73 — .NET 10 Worker Service scaffold (long-running background service).</summary>
public sealed class DotNetWorkerScaffold : IProjectTemplate
{
    public string Id => "dotnet/worker";
    public string Language => "dotnet";
    public string Kind => "worker";
    public string Name => ".NET Worker Service";
    public string Description => ".NET 10 hosted worker service with IHostedService, structured logging, and graceful shutdown.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pascal = ScaffoldHelpers.ToPascalCase(context.ProjectName);
        var mainGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();

        return
        [
            new($"{pascal}.csproj", $$"""
                <Project Sdk="Microsoft.NET.Sdk.Worker">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <AnalysisMode>All</AnalysisMode>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.*" />
                  </ItemGroup>
                </Project>
                """),

            new("Program.cs", $$"""
                using {{pascal}};

                var builder = Host.CreateApplicationBuilder(args);
                builder.Services.AddHostedService<Worker>();

                var host = builder.Build();
                host.Run();
                """),

            new("Worker.cs", $$"""
                namespace {{pascal}};

                public sealed partial class Worker(ILogger<Worker> logger) : BackgroundService
                {
                    [LoggerMessage(Level = LogLevel.Information, Message = "Worker running at {Time}")]
                    private static partial void LogRunning(ILogger logger, DateTimeOffset time);

                    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
                    {
                        while (!stoppingToken.IsCancellationRequested)
                        {
                            LogRunning(logger, DateTimeOffset.UtcNow);
                            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                        }
                    }
                }
                """),

            new("appsettings.json", """
                {
                  "Logging": {
                    "LogLevel": {
                      "Default": "Information",
                      "Microsoft.Hosting.Lifetime": "Information"
                    }
                  }
                }
                """),

            new($"{pascal}.sln", $$"""
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                VisualStudioVersion = 17.0.31903.59
                MinimumVisualStudioVersion = 10.0.40219.1
                Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "{{pascal}}", "{{pascal}}.csproj", "{{mainGuid}}"
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
                  build:
                    runs-on: ubuntu-latest
                    steps:
                      - uses: actions/checkout@v4
                      - uses: actions/setup-dotnet@v4
                        with:
                          dotnet-version: '10.x'
                      - run: dotnet restore
                      - run: dotnet build --no-restore
                      - run: dotnet publish -c Release -o publish/
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

                A .NET 10 worker service.

                ## Run

                ```bash
                dotnet run
                ```

                ## Publish

                ```bash
                dotnet publish -c Release -o publish/
                ```

                The worker runs in the background, logging a heartbeat every 5 seconds.
                Press Ctrl+C to stop.
                """),
        ];
    }
}
