using Loom.Config;
using Loom.Core.Text;

namespace Loom.Core.Pipeline;

public static class FileManager
{
    public const string LoomExtension = ".loom";
    private const string IncludeFolderName = "include";
    
    public static void WriteIncludeFolder(string projectDirectory)
    {
        var source = Path.Combine(AppContext.BaseDirectory, IncludeFolderName);

        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException(
                $"Could not locate bundled '{IncludeFolderName}' directory at '{source}'.");

        var destination = Path.Combine(projectDirectory, IncludeFolderName);

        CopyDirectory(source, destination);
    }

    /// <summary>Writes the file's rendered Luau, skipping the write entirely when it would be byte-identical to what's already on disk.</summary>
    /// <returns>Whether the file was actually written.</returns>
    public static bool WriteCompiledFile(CompiledFile file)
    {
        if (File.Exists(file.Path) && File.ReadAllText(file.Path) == file.RenderedLuau)
            return false;

        var directory = Path.GetDirectoryName(file.Path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(file.Path, file.RenderedLuau);
        Log.Info($"Wrote {file.Path}");
        return true;
    }

    public static string GetOutputPath(SourceFile file, LoomConfig config)
    {
        var relativePath = Path.GetRelativePath(config.Files.SourceDirectory, file.AbsolutePath);
        var outputPath = Path.Combine(config.Files.OutputDirectory, relativePath);
        return Path.ChangeExtension(outputPath, ".luau");
    }

    public static bool IsLoomFile(string path) => Path.GetExtension(path) == LoomExtension;

    public static SourceFile LoadSingle(string path) => new(Path.GetFullPath(path));

    public static List<SourceFile> LoadDirectory(string directoryPath) => LoadDirectory(directoryPath, SearchOption.AllDirectories);

    private static List<SourceFile> LoadDirectory(string directoryPath, SearchOption searchOption) =>
        !string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath)
            ? Directory.GetFiles(directoryPath, $"*{LoomExtension}", searchOption).Select(LoadSingle).ToList()
            : [];
    
    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destinationFile = Path.Combine(destination, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite: true);
        }
    }
}