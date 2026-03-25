using AIHomeAssistant.Tests.Helpers;

namespace AIHomeAssistant.Tests.Integration;

/// <summary>
/// Shared collection fixture that provides ONE TestWebApplicationFactory
/// across all integration test classes, avoiding Serilog static-logger conflicts.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<TestWebApplicationFactory>
{
    // This class intentionally empty — it's a marker for the collection.
}
