namespace FromModel.Tests;

public class PropertyModifierTests
{
    [Fact]
    public void PreservesRequiredModifier()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class MyModel { public required string Name { get; set; } }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("public required string Name { get; set; }", source);
    }

    [Fact]
    public void NonRequiredPropertyHasNoRequiredModifier()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class MyModel { public string Name { get; set; } = ""; }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.DoesNotContain("required", source);
    }

    [Fact]
    public void PreservesStringEmptyInitializer()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class MyModel { public string Name { get; set; } = string.Empty; }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("public string Name { get; set; } = string.Empty;", source);
    }

    [Fact]
    public void PreservesLiteralInitializer()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class MyModel { public int Count { get; set; } = 42; }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("public int Count { get; set; } = 42;", source);
    }

    [Fact]
    public void PropertiesWithoutInitializerHaveNoAssignment()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class MyModel { public int Count { get; set; } }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("public int Count { get; set; }", source);
        Assert.DoesNotContain("=", source);
    }
}
