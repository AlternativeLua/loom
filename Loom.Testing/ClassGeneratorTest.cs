using Loom.TypeGenerator.ApiTypes;
using Loom.TypeGenerator.Generators;

namespace Loom.Testing;

public class ClassGeneratorTest
{
    [Fact]
    public void GetParameterNames_RenamesDuplicate()
    {
        var parameters = new[]
        {
            new Parameter { Name = "part" },
            new Parameter { Name = "other" },
            new Parameter { Name = "part" }
        };

        var names = ClassGenerator.GetParameterNames(parameters);
        Assert.Equal(["part", "other", "part0"], names);
    }
}
