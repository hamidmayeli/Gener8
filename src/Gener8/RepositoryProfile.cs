using Gener8.Contexts;

namespace Gener8;

// Everything that differs between repository backends, in one place per backend.
//
// Adding a backend means adding a profile and one line to For(), rather than finding every
// `if (repository == RepositoryKind.X)` scattered across type resolution and emission.
internal abstract class RepositoryProfile
{
    public static RepositoryProfile For(RepositoryKind kind) => kind switch
    {
        RepositoryKind.DynamoDb => new DynamoDbProfile(),
        RepositoryKind.MongoDb => new MongoDbProfile(),
        RepositoryKind.Custom => new CustomProfile(),
        _ => new NoRepositoryProfile()
    };

    // Namespaces the generated DTO file needs, in emission order.
    public virtual string[] DtoUsings => [];

    // True when the backend materialises DTO instances itself and therefore cannot handle an
    // abstract collection interface property (IReadOnlyList<T>, ISet<T>, ...).
    public virtual bool RemapsAbstractCollections => false;

    // The attribute line to emit above an enum (or enum-collection) DTO property, if any.
    public virtual string? EnumPropertyAttribute(PropertyTypeData typeData) => null;

    // Null when the backend generates no concrete repository class.
    public virtual string? RepositoryBaseType => null;

    public virtual string? RepositoryContextType => null;

    public bool GeneratesRepository => RepositoryBaseType is not null;

    // The arguments forwarded to the base repository constructor.
    public virtual string BaseConstructorArguments(string dtoClassName) => "context";

    private sealed class NoRepositoryProfile : RepositoryProfile;

    private sealed class DynamoDbProfile : RepositoryProfile
    {
        public override string[] DtoUsings => ["Amazon.DynamoDBv2.DataModel", "Gener8.Converters"];

        // The AWS SDK instantiates the DTO, so collection properties need a concrete type.
        public override bool RemapsAbstractCollections => true;

        public override string? RepositoryBaseType => "Gener8.DynamoDbRepository";

        public override string? RepositoryContextType => "IDynamoDbRepositoryContext";

        public override string EnumPropertyAttribute(PropertyTypeData typeData)
            => $"[DynamoDBProperty(typeof({ConverterType(typeData)}))]";

        private static string ConverterType(PropertyTypeData typeData)
        {
            if (typeData.EnumCollectionElementType is { } elementType)
                return elementType.EndsWith("?")
                    ? $"NullableEnumListToStringListConverter<{TypeNames.StripNullable(elementType)}>"
                    : $"EnumListToStringListConverter<{elementType}>";

            return typeData.IsNullable
                ? $"NullableEnumToStringConverter<{TypeNames.StripNullable(typeData.Type)}>"
                : $"EnumToStringConverter<{typeData.Type}>";
        }
    }

    // RemapsAbstractCollections stays false: the MongoDB driver can instantiate abstract
    // collection types, so DTOs keep the model's declared collection type.
    private sealed class MongoDbProfile : RepositoryProfile
    {
        public override string[] DtoUsings => ["MongoDB.Bson", "MongoDB.Bson.Serialization.Attributes"];

        public override string? RepositoryBaseType => "Gener8.MongoDbRepository";

        public override string? RepositoryContextType => "IMongoDbRepositoryContext";

        public override string EnumPropertyAttribute(PropertyTypeData typeData)
            => "[BsonRepresentation(BsonType.String)]";

        // MongoDB names the collection after the DTO type.
        public override string BaseConstructorArguments(string dtoClassName)
            => $"context, \"{dtoClassName}\"";
    }

    private sealed class CustomProfile : RepositoryProfile
    {
        public override string? RepositoryBaseType => "Gener8.RepositoryBase";

        public override string? RepositoryContextType => "IRepositoryContext";
    }
}
