namespace Gener8;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class IgnoreTypeMappingAttribute(System.Type ignoredType) : System.Attribute
{
    public System.Type IgnoredType { get; } = ignoredType;
}
