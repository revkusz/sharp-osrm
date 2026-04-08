using Xunit;

namespace Sharposrm.Tests.Fixtures;

/// <summary>
/// xUnit collection definition that ensures all test classes in this collection
/// share a single <see cref="MonacoDataFixture"/> instance.
/// <para>
/// Add <c>[Collection("MonacoDataSet")]</c> to any test class that needs
/// Monaco OSRM test data (CH or MLD paths).
/// </para>
/// </summary>
[CollectionDefinition("MonacoDataSet")]
public class MonacoFixtureCollection : ICollectionFixture<MonacoDataFixture>
{
    // This class is never instantiated. xUnit uses it only for
    // the [CollectionDefinition] and ICollectionFixture<> mapping.
}
