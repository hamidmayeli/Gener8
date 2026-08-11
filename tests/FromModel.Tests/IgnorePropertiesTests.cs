namespace FromModel.Tests;

public class IgnorePropertiesTests
{
    [Fact]
    public void IgnoreExcludesNamedProperties()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class MyModel
            {
                public string Name { get; set; } = "";
                public string InternalCode { get; set; } = "";
                public int Age { get; set; }
            }
            [FromModel(typeof(MyModel), Ignore = [nameof(MyModel.InternalCode)])]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("public string Name", source);
        Assert.Contains("public int Age", source);
        Assert.DoesNotContain("InternalCode", source);
    }

    [Fact]
    public void IgnoreExcludesMultipleNamedProperties()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class MyModel
            {
                public string Name { get; set; } = "";
                public string InternalCode { get; set; } = "";
                public System.DateTime AuditTimestamp { get; set; }
                public int Age { get; set; }
            }
            [FromModel(typeof(MyModel), Ignore = [nameof(MyModel.InternalCode), nameof(MyModel.AuditTimestamp)])]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("public string Name", source);
        Assert.Contains("public int Age", source);
        Assert.DoesNotContain("InternalCode", source);
        Assert.DoesNotContain("AuditTimestamp", source);
    }

    [Fact]
    public void EmptyIgnoreIncludesAllProperties()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class MyModel { public string Name { get; set; } = ""; public int Age { get; set; } }
            [FromModel(typeof(MyModel), Ignore = [])]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("public string Name", source);
        Assert.Contains("public int Age", source);
    }

    [Fact]
    public void DefaultIgnoreIncludesAllProperties()
    {
        var results = GeneratorDriver.Run("""
            using FromModel;
            public class MyModel { public string Name { get; set; } = ""; public int Age { get; set; } }
            [FromModel(typeof(MyModel))]
            public partial class MyDto { }
            """);

        var source = Assert.Single(results, r => r.Key == "MyDto.g.cs").Value;
        Assert.Contains("public string Name", source);
        Assert.Contains("public int Age", source);
    }
}
