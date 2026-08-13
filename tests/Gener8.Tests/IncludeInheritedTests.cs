namespace Gener8.Tests;

public class IncludeInheritedTests
{
    [Fact]
    public void DefaultBehavior_ExcludesBaseProperties()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Base { public string BaseName { get; set; } = ""; }
            public class Derived : Base { public string DerivedName { get; set; } = ""; }
            [FromModel(typeof(Derived))]
            public partial class DerivedDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "DerivedDto.g.cs").Value;
        Assert.Contains("DerivedName", source);
        Assert.DoesNotContain("BaseName", source);
    }

    [Fact]
    public void IncludeInherited_CopiesBaseProperties()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Base { public string BaseName { get; set; } = ""; }
            public class Derived : Base { public string DerivedName { get; set; } = ""; }
            [FromModel(typeof(Derived), IncludeInherited = true)]
            public partial class DerivedDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "DerivedDto.g.cs").Value;
        Assert.Contains("DerivedName", source);
        Assert.Contains("BaseName", source);
    }

    [Fact]
    public void IncludeInherited_MultiLevelChain()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class GrandBase { public int GrandValue { get; set; } }
            public class Base : GrandBase { public string BaseName { get; set; } = ""; }
            public class Derived : Base { public string DerivedName { get; set; } = ""; }
            [FromModel(typeof(Derived), IncludeInherited = true)]
            public partial class DerivedDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "DerivedDto.g.cs").Value;
        Assert.Contains("DerivedName", source);
        Assert.Contains("BaseName", source);
        Assert.Contains("GrandValue", source);
    }

    [Fact]
    public void IncludeInherited_MostDerivedPropertyWins()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Base { public virtual string Name { get; set; } = "base"; }
            public class Derived : Base { public override string Name { get; set; } = "derived"; }
            [FromModel(typeof(Derived), IncludeInherited = true)]
            public partial class DerivedDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "DerivedDto.g.cs").Value;
        Assert.Contains("public string Name { get; set; } = \"derived\"", source);
        var count = source.Split("public string Name").Length - 1;
        Assert.Equal(1, count);
    }
}
