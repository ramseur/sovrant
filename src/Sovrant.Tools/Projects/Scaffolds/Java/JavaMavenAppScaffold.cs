using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Java;

/// <summary>Phase 73 — Java Maven application scaffold (Java 21, JUnit 5).</summary>
public sealed class JavaMavenAppScaffold : IProjectTemplate
{
    public string Id => "java/maven-app";
    public string Language => "java";
    public string Kind => "maven-app";
    public string Name => "Java Maven App";
    public string Description => "Java 21 Maven application with JUnit 5 tests and surefire plugin.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pascal = ScaffoldHelpers.ToPascalCase(context.ProjectName);
        var pkg = ScaffoldHelpers.ToJavaPackage(context.ProjectName);
        var artifactId = ScaffoldHelpers.ToKebabCase(context.ProjectName);
        var pkgPath = pkg.Replace('.', '/');

        return
        [
            new("pom.xml", $$"""
                <?xml version="1.0" encoding="UTF-8"?>
                <project xmlns="http://maven.apache.org/POM/4.0.0"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xsi:schemaLocation="http://maven.apache.org/POM/4.0.0 https://maven.apache.org/xsd/maven-4.0.0.xsd">
                  <modelVersion>4.0.0</modelVersion>

                  <groupId>com.example</groupId>
                  <artifactId>{{artifactId}}</artifactId>
                  <version>0.1.0</version>
                  <packaging>jar</packaging>

                  <properties>
                    <maven.compiler.release>21</maven.compiler.release>
                    <project.build.sourceEncoding>UTF-8</project.build.sourceEncoding>
                    <junit.version>5.11.0</junit.version>
                  </properties>

                  <dependencies>
                    <dependency>
                      <groupId>org.junit.jupiter</groupId>
                      <artifactId>junit-jupiter</artifactId>
                      <version>${junit.version}</version>
                      <scope>test</scope>
                    </dependency>
                  </dependencies>

                  <build>
                    <plugins>
                      <plugin>
                        <groupId>org.apache.maven.plugins</groupId>
                        <artifactId>maven-surefire-plugin</artifactId>
                        <version>3.5.0</version>
                      </plugin>
                    </plugins>
                  </build>
                </project>
                """),

            new($"src/main/java/{pkgPath}/App.java", $$"""
                package {{pkg}};

                public class App {
                    public static String greet(String name) {
                        return "Hello, " + name + "!";
                    }

                    public static void main(String[] args) {
                        String name = args.length > 0 ? args[0] : "World";
                        System.out.println(greet(name));
                    }
                }
                """),

            new($"src/test/java/{pkgPath}/AppTest.java", $$"""
                package {{pkg}};

                import org.junit.jupiter.api.Test;
                import static org.junit.jupiter.api.Assertions.*;

                class AppTest {
                    @Test
                    void greetReturnsSalutation() {
                        assertEquals("Hello, Sovrant!", App.greet("Sovrant"));
                    }

                    @Test
                    void greetIncludesName() {
                        assertTrue(App.greet("World").contains("World"));
                    }
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
                      - run: mvn -B package
                """),

            new(".gitignore", """
                target/
                .classpath
                .project
                .settings/
                .idea/
                *.iml
                .env
                """),

            new("README.md", $$"""
                # {{pascal}}

                A Java 21 Maven application.

                ## Build

                ```bash
                mvn package
                ```

                ## Run

                ```bash
                mvn exec:java -Dexec.mainClass="{{pkg}}.App" -Dexec.args="World"
                ```

                ## Test

                ```bash
                mvn test
                ```
                """),
        ];
    }
}
