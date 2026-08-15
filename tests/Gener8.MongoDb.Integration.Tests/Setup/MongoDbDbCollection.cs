namespace Gener8.MongoDb.Integration.Tests.Setup;

// The string name must perfectly match across all test classes
[CollectionDefinition("Shared DynamoDb Collection")]
public class MongoDbDbCollection : ICollectionFixture<TestFixture>
{
    // This class has no code and is never instantiated.
    // Its only purpose is to apply the [CollectionDefinition] attribute
    // and link it to the ICollectionFixture interface.
}
