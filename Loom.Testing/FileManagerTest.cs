using Loom.Config;
using Loom.Core.Pipeline;
using Loom.Core.Text;

namespace Loom.Testing;

[Collection("Assembly")]
public class FileManagerTest
{
    [Fact]
    public void Loads_Single()
    {
        var file = FileManager.LoadSingle($"{AssemblyFixture.Snapshots}/src/basic_binary.loom");
        Assert.Equal("basic_binary.loom", file.Name);
        Assert.Equal($"{AssemblyFixture.Snapshots}{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}basic_binary.loom", file.RelativePath());
        Assert.Equal($"src{Path.DirectorySeparatorChar}basic_binary.loom", file.RelativePath(AssemblyFixture.Snapshots));
        Assert.Equal("basic_binary.loom", file.RelativePath(AssemblyFixture.Snapshots + "/src"));
        Assert.Equal("1 + 2", file.SourceText);
    }

    [Fact]
    public void GetOutputPath_SourceDirectoryLeafRepeatedInPath_DoesNotCorruptPath()
    {
        var sourceDirectory = Path.Combine("proj", "src", "src");
        var outputDirectory = Path.Combine("proj", "src", "dist");
        var config = new LoomConfig { Files = new FilesConfig { SourceDirectory = sourceDirectory, OutputDirectory = outputDirectory } };
        var file = new SourceFile(Path.Combine(sourceDirectory, "foo.loom"), "");

        var outputPath = FileManager.GetOutputPath(file, config);
        Assert.Equal(Path.Combine(outputDirectory, "foo.luau"), outputPath);
    }
}