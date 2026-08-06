using CommandLine;

namespace Loom.CLI;

internal abstract class ProjectCommand
{
    [Value(0, MetaName = "directory", Default = ".", HelpText = "The project directory.")]
    public string Directory { get; init; } = ".";
}

[Verb("build", HelpText = "Build a Loom project.")]
internal sealed class BuildOptions : ProjectCommand;

[Verb("watch", HelpText = "Build a Loom project and watch for changes.")]
internal sealed class WatchOptions : ProjectCommand;

[Verb("new", HelpText = "Create a new Loom project.")]
internal sealed class NewOptions : ProjectCommand;