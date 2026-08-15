namespace Gener8.Tests;

public class UserDeclaredPropertyTests
{
    [Fact]
    public void SkipsGenerationForUserDeclaredProperty()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product
            {
                public required System.Guid Id { get; set; }
                public required string Name { get; set; }
            }
            [FromModel(typeof(Product))]
            public partial class ProductDto
            {
                public required System.Guid Id { get; set; }
            }
            """);

        var model = results["ProductDto.g.cs"];
        Assert.DoesNotContain("System.Guid Id", model);
        Assert.Contains("public required string Name { get; set; }", model);
    }

    [Fact]
    public void IncludesUserDeclaredPropertyInToModel()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product
            {
                public required System.Guid Id { get; set; }
                public required string Name { get; set; }
            }
            [FromModel(typeof(Product))]
            public partial class ProductDto
            {
                public required System.Guid Id { get; set; }
            }
            """);

        var ext = results["ProductDtoExtensions.g.cs"];
        Assert.Contains("Id = dto.Id,", ext);
        Assert.Contains("Name = dto.Name,", ext);
    }

    [Fact]
    public void IncludesUserDeclaredPropertyInToDto()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product
            {
                public required System.Guid Id { get; set; }
                public required string Name { get; set; }
            }
            [FromModel(typeof(Product))]
            public partial class ProductDto
            {
                public required System.Guid Id { get; set; }
            }
            """);

        var ext = results["ProductDtoExtensions.g.cs"];
        Assert.Contains("Id = model.Id,", ext);
        Assert.Contains("Name = model.Name,", ext);
    }

    [Fact]
    public void MultipleUserDeclaredPropertiesAreAllSkippedInGeneration()
    {
        var results = GeneratorDriver.Run("""
            using Gener8;
            public class Product
            {
                public System.Guid Id { get; set; }
                public string Name { get; set; } = "";
                public string Description { get; set; } = "";
            }
            [FromModel(typeof(Product))]
            public partial class ProductDto
            {
                public System.Guid Id { get; set; }
                public string Name { get; set; } = "";
            }
            """);

        var model = results["ProductDto.g.cs"];
        Assert.DoesNotContain("System.Guid Id", model);
        Assert.DoesNotContain("string Name", model);
        Assert.Contains("public string Description { get; set; }", model);
    }
}
