using Xunit;

namespace Sovrant.Api.Tests;

/// <summary>
/// Tests that read or change process-wide environment variables (OPENROUTER_API_KEY,
/// OLLAMA_BASE_URL, model overrides, …) run one at a time: in parallel, one test's temporary
/// value leaks into another's credential resolution and makes it flaky.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection
{
    public const string Name = "Process environment";
}
