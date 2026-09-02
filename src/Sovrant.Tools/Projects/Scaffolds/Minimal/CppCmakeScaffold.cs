using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects.Scaffolds.Minimal;

/// <summary>Phase 73 — C/C++ CMake scaffold (C++17, GoogleTest).</summary>
public sealed class CppCmakeScaffold : IProjectTemplate
{
    public string Id => "cpp/cmake";
    public string Language => "cpp";
    public string Kind => "cmake";
    public string Name => "C++ CMake App";
    public string Description => "C++17 CMake project with GoogleTest unit tests.";
    public IReadOnlyList<ScaffoldParameter> Parameters => [];

    public IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var snake = ScaffoldHelpers.ToSnakeCase(context.ProjectName);
        var kebab = ScaffoldHelpers.ToKebabCase(context.ProjectName);
        var pascal = ScaffoldHelpers.ToPascalCase(context.ProjectName);

        return
        [
            new("CMakeLists.txt", $$"""
                cmake_minimum_required(VERSION 3.20)
                project({{snake}} VERSION 0.1.0 LANGUAGES CXX)

                set(CMAKE_CXX_STANDARD 17)
                set(CMAKE_CXX_STANDARD_REQUIRED ON)
                set(CMAKE_CXX_EXTENSIONS OFF)

                # Main library (logic shared between app and tests)
                add_library({{snake}}_lib src/{{snake}}.cpp)
                target_include_directories({{snake}}_lib PUBLIC include)

                # Executable
                add_executable({{kebab}} src/main.cpp)
                target_link_libraries({{kebab}} PRIVATE {{snake}}_lib)

                # Tests
                include(FetchContent)
                FetchContent_Declare(
                  googletest
                  URL https://github.com/google/googletest/archive/refs/tags/v1.15.2.zip
                  DOWNLOAD_EXTRACT_TIMESTAMP TRUE
                )
                set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)
                FetchContent_MakeAvailable(googletest)

                enable_testing()

                add_executable({{snake}}_tests tests/{{snake}}_test.cpp)
                target_link_libraries({{snake}}_tests PRIVATE {{snake}}_lib GTest::gtest_main)

                include(GoogleTest)
                gtest_discover_tests({{snake}}_tests)
                """),

            new($"include/{snake}.h", $$"""
                #pragma once
                #include <string>

                namespace {{pascal}} {

                /// Returns a greeting for the given name.
                std::string greet(const std::string& name);

                }  // namespace {{pascal}}
                """),

            new($"src/{snake}.cpp", $$"""
                #include "{{snake}}.h"

                namespace {{pascal}} {

                std::string greet(const std::string& name) {
                    return "Hello, " + name + "!";
                }

                }  // namespace {{pascal}}
                """),

            new("src/main.cpp", $$"""
                #include <iostream>
                #include "{{snake}}.h"

                int main(int argc, char* argv[]) {
                    const std::string name = argc > 1 ? argv[1] : "World";
                    std::cout << {{pascal}}::greet(name) << '\n';
                    return 0;
                }
                """),

            new($"tests/{snake}_test.cpp", $$"""
                #include <gtest/gtest.h>
                #include "{{snake}}.h"

                TEST(GreetTest, ReturnsSalutation) {
                    EXPECT_EQ({{pascal}}::greet("Sovrant"), "Hello, Sovrant!");
                }

                TEST(GreetTest, IncludesName) {
                    const auto result = {{pascal}}::greet("World");
                    EXPECT_NE(result.find("World"), std::string::npos);
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
                      - run: cmake -B build -DCMAKE_BUILD_TYPE=Debug
                      - run: cmake --build build
                      - run: ctest --test-dir build --output-on-failure
                """),

            new(".gitignore", """
                build/
                cmake-build-*/
                .cmake/
                CMakeFiles/
                CMakeCache.txt
                .env
                """),

            new("README.md", $$"""
                # {{kebab}}

                A C++17 CMake application.

                ## Build

                ```bash
                cmake -B build -DCMAKE_BUILD_TYPE=Release
                cmake --build build
                ```

                ## Run

                ```bash
                ./build/{{kebab}} World
                ```

                ## Test

                ```bash
                cmake -B build -DCMAKE_BUILD_TYPE=Debug
                cmake --build build
                ctest --test-dir build --output-on-failure
                ```
                """),
        ];
    }
}
