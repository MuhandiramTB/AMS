namespace TAMS.Integration.Tests;

/// <summary>
/// Marker that shares one <see cref="TamsWebApplicationFactory"/> (and its test
/// database) across all integration test classes. The class body must stay empty —
/// xunit only uses it to associate the collection name with the fixture type. DB
/// reset is handled by the factory's own IAsyncLifetime.
/// </summary>
[CollectionDefinition("integration")]
public sealed class IntegrationCollection : ICollectionFixture<TamsWebApplicationFactory>
{
}
