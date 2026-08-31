namespace Gener8;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FromModelAttribute(System.Type modelType) : System.Attribute
{
    public System.Type ModelType { get; } = modelType;
    public string[] Ignore { get; set; } = [];
    public bool IncludeInherited { get; set; }
    public string[] Flatten { get; set; } = [];
    public FlattenPrefix FlattenPrefix { get; set; }
    public RepositoryType Repository { get; set; }
    public string[] DtoNamespaces { get; set; } = [];
    public string[] ForceNullable { get; set; } = [];
}
