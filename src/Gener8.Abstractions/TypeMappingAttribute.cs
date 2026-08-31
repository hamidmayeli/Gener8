namespace Gener8;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class TypeMappingAttribute(System.Type sourceType, System.Type targetType) : System.Attribute
{
    public System.Type SourceType { get; } = sourceType;
    public System.Type TargetType { get; } = targetType;
}
