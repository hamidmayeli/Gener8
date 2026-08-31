namespace Gener8;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RenamePropertyAttribute(string sourceName, string targetName) : System.Attribute
{
    public string SourceName { get; } = sourceName;
    public string TargetName { get; } = targetName;
}
