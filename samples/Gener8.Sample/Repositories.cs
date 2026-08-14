namespace Gener8.Sample;

public class DynamoDbContent
{
    public string? Key { get; set; }
}

[FromModel(typeof(DynamoDbContent), Repository = RepositoryType.DynamoDb)]
public partial class DynamoDbContentDto { }

public class MongoDbContent
{
    public string? Key { get; set; }
}

[FromModel(typeof(MongoDbContent), Repository = RepositoryType.MongoDb)]
public partial class MongoDbContentDto { }


/**/
