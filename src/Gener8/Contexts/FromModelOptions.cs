using Gener8.ContextBuilders;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Gener8.Contexts;

// The [FromModel] named arguments that shape which properties end up on the DTO and how they
// are named. A synthesised auto-DTO has no attribute, so every option falls back to its default.
internal sealed record FromModelOptions(
    HashSet<string> Ignored,
    HashSet<string> Flattened,
    HashSet<string> ForceNullable,
    FlattenPrefixMode FlattenPrefix,
    bool IncludeInherited,
    OnlyIncludeFilter OnlyInclude)
{
    public static FromModelOptions Read(AttributeData? attribute, IReadOnlyCollection<string>? onlyIncludePaths)
        => new(
            AttributeReader.GetStringSet(attribute, "Ignore"),
            AttributeReader.GetStringSet(attribute, "Flatten"),
            AttributeReader.GetStringSet(attribute, "ForceNullable"),
            AttributeReader.GetEnum(attribute, "FlattenPrefix", FlattenPrefixMode.Parent),
            AttributeReader.GetBool(attribute, "IncludeInherited"),
            OnlyIncludeFilter.Parse(attribute, onlyIncludePaths));

    // The DTO-side name for a property inlined from a nested type.
    public string FlattenedName(string parentName, string nestedName) => FlattenPrefix switch
    {
        FlattenPrefixMode.Parent => parentName + nestedName,
        FlattenPrefixMode.Gaped => parentName + "_" + nestedName,
        _ => nestedName
    };
}
