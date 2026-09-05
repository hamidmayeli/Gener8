using Gener8.Contexts;
using Microsoft.CodeAnalysis;

namespace Gener8.ContextBuilders.TypeMapping;

// A supported dictionary's key or value type is remapped:
// Dictionary<ComplexKey, int> -> Dictionary<ComplexKeyDto, int>.
// Returns false when neither key nor value needs remapping (direct copy).
internal sealed class DictionaryMappingRule : ITypeMappingRule
{
    public bool TryApply(in TypeMappingContext context, out MappedType mapped)
    {
        if (context.Type is not INamedTypeSymbol { IsGenericType: true, Arity: 2 } namedType
            || !KnownCollections.IsKnownDictionary(namedType))
        {
            mapped = default;
            return false;
        }

        var keyArg = namedType.TypeArguments[0];
        var valueArg = namedType.TypeArguments[1];

        var keyMapped = context.Mappings.TryGetMapping(keyArg, out var mappedKey);
        var valueMapped = context.Mappings.TryGetElementMapping(valueArg, out var mappedValue);

        if (!keyMapped && !valueMapped)
        {
            mapped = default;
            return false;
        }

        var finalKey = keyMapped ? mappedKey! : keyArg.ToDisplayString();
        var finalValue = valueMapped ? mappedValue! : valueArg.ToDisplayString();

        var typeDisplay = TypeNames.WithNullable(
            KnownCollections.Dictionary(finalKey, finalValue), context.IsNullable);

        string? keyToDtoMethodName = keyMapped
            ? TypeNames.DtoMethodName(keyArg.ToDisplayString(), mappedKey!)
            : null;
        string? valueToDtoMethodName = valueMapped
            ? TypeNames.DtoMethodName(valueArg.ToDisplayString(), mappedValue!)
            : null;

        mapped = new(
            typeDisplay,
            IsGeneric: true,
            ElementType: valueMapped ? finalValue : null,
            valueToDtoMethodName,
            IsDictionary: true,
            DictionaryMappedKeyType: keyMapped ? finalKey : null,
            DictionaryKeyToDtoMethodName: keyToDtoMethodName);

        return true;
    }
}
