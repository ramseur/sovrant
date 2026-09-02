using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Minimal;

/// <summary>Phase 73 — Kotlin Gradle console scaffold (JVM 21, JUnit 5).</summary>
public sealed class KotlinConsoleScaffold : IProjectTemplate
{
    public string Id => "kotlin/console";
    public string Language => "kotlin";
    public string Kind => "console";
    public string Name => "Kotlin Console App";
    public string Description => "Kotlin console application with Gradle Kotlin DSL and JUnit 5 tests.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pascal = ScaffoldHelpers.ToPascalCase(context.ProjectName);
        var pkg = ScaffoldHelpers.ToJavaPackage(context.ProjectName);
        var pkgPath = pkg.Replace('.', '/');

        return
        [
            new("build.gradle.kts", $$"""
                plugins {
                    kotlin("jvm") version "2.1.0"
                    application
                }

                group = "com.example"
                version = "0.1.0"

                repositories { mavenCentral() }

                dependencies {
                    testImplementation("org.junit.jupiter:junit-jupiter:5.11.0")
                    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
                }

                application { mainClass = "{{pkg}}.MainKt" }

                tasks.test { useJUnitPlatform() }
                """),

            new("settings.gradle.kts", $$"""
                rootProject.name = "{{ScaffoldHelpers.ToKebabCase(context.ProjectName)}}"
                """),

            new($"src/main/kotlin/{pkgPath}/Main.kt", $$"""
                package {{pkg}}

                fun greet(name: String): String = "Hello, $name!"

                fun main(args: Array<String>) {
                    val name = args.firstOrNull() ?: "World"
                    println(greet(name))
                }
                """),

            new($"src/test/kotlin/{pkgPath}/MainTest.kt", $$"""
                package {{pkg}}

                import org.junit.jupiter.api.Assertions.*
                import org.junit.jupiter.api.Test

                class MainTest {
                    @Test
                    fun greetReturnsSalutation() = assertEquals("Hello, Sovrant!", greet("Sovrant"))

                    @Test
                    fun greetIncludesName() = assertTrue(greet("World").contains("World"))
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
                      - uses: actions/setup-java@v4
                        with:
                          java-version: '21'
                          distribution: 'temurin'
                      - run: ./gradlew build
                """),

            new(".gitignore", """
                .gradle/
                build/
                .idea/
                *.iml
                """),

            new("README.md", $$"""
                # {{pascal}}

                A Kotlin console application.

                ## Run

                ```bash
                ./gradlew run --args="World"
                ```

                ## Test

                ```bash
                ./gradlew test
                ```
                """),
        ];
    }
}
