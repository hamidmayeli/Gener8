namespace Gener8.Tests;

public class FromModelGeneratorTests
{
    [Fact]
    public void CopiesPublicGetSetProperties()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class MyModel { public string Name { get; set; } = ""; public int Age { get; set; } }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("public string Name { get; set; }", source);
        Assert.Contains("public int Age { get; set; }", source);
    }

    [Fact]
    public void SkipsGetOnlyProperty()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class MyModel { public string Id { get; } = ""; public string Name { get; set; } = ""; }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.DoesNotContain("Id", source);
        Assert.Contains("public string Name { get; set; }", source);
    }

    [Fact]
    public void PreservesInitOnlyProperty()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class MyModel { public string Name { get; init; } = ""; }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("public string Name { get; init; }", source);
        Assert.DoesNotContain("set;", source);
    }

    [Fact]
    public void ExcludesNonPublicProperties()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class MyModel
            {
                public string Visible { get; set; } = "";
                internal string Hidden { get; set; } = "";
                private string Secret { get; set; } = "";
            }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("Visible", source);
        Assert.DoesNotContain("Hidden", source);
        Assert.DoesNotContain("Secret", source);
    }

    [Fact]
    public void ExcludesStaticProperties()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class MyModel
            {
                public string Instance { get; set; } = "";
                public static string StaticProp { get; set; } = "";
            }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("Instance", source);
        Assert.DoesNotContain("StaticProp", source);
    }

    [Fact]
    public void EmitsNamespaceWhenDtoIsNamespaced()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class MyModel { public int Value { get; set; } }
            namespace My.App
            {
                [FromModel(typeof(MyModel))]
                public partial class MyDto { }
            }
            """);

        var source = Assert.Single(results, r => r.Key == "My.App.MyDto.g.cs").Value;
        Assert.Contains("namespace My.App", source);
        Assert.Contains("public partial class MyDto", source);
        Assert.Contains("public int Value { get; set; }", source);
    }

    [Fact]
    public void PreservesInternalAccessibility()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class MyModel { public string Name { get; set; } = ""; }
            [FromModel(typeof(MyModel))]
            internal partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("internal partial class MyDto", source);
    }

    [Fact]
    public void EmitsEmptyClassWhenModelHasNoProperties()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class MyModel { }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("public partial class MyDto", source);
    }

    // -----------------------------------------------------------------------
    // Nullable context
    // -----------------------------------------------------------------------

    [Fact]
    public void NullableDisabled_DoesNotEmitNullableDirective()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class MyModel { public string Name { get; set; } = ""; }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        Assert.DoesNotContain("#nullable enable", results["MyDto.g.cs"]);
        Assert.DoesNotContain("#nullable enable", results["MyDtoExtensions.g.cs"]);
    }

    [Fact]
    public void NullableEnabled_EmitsNullableDirective()
    {
        var results = GeneratorDriver.RunWithNullable("""
            using Gener8;
            public class MyModel { public string Name { get; set; } = ""; }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        Assert.Contains("#nullable enable", results["MyDto.g.cs"]);
        Assert.Contains("#nullable enable", results["MyDtoExtensions.g.cs"]);
    }

    [Fact]
    public void NullableDisabled_PreservesNullableValueTypes()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class MyModel { public int? Count { get; set; } public System.DateTime? CreatedAt { get; set; } }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = results["MyDto.g.cs"];
        Assert.Contains("int?", source);
        Assert.Contains("DateTime?", source);
    }
}
