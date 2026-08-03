using System.Diagnostics;

namespace Loom.Testing;

/// <summary>
///     Executes the emitted serializers under a Luau runtime instead of only reading them. Reading the
///     output cannot tell you that a nested struct comes back the wrong shape, that a local shadows the
///     accumulator it is meant to fill, or that a buffer was sized a byte short - all of which shipped
///     past the snapshot suite and were caught here.
/// </summary>
/// <remarks>
///     Each case pairs a Loom snapshot with a Luau assertion body of the same name under
///     <c>Runtime/</c>. The two are assembled with <c>Runtime/prelude.luau</c>, which stubs the Roblox
///     surface the serializers touch and the runtime helpers they call, then run through Lune. Tests skip
///     when no runtime is installed so a local <c>dotnet test</c> stays self-contained; CI installs one.
/// </remarks>
[Collection("Assembly")]
public class SerializationRuntimeTest
{
    private static readonly string RuntimeDirectory = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}Runtime";

    public static IEnumerable<TheoryDataRow<string>> Cases =>
        Directory.EnumerateFiles(RuntimeDirectory, "serialize_*.luau")
            .Select(path => new TheoryDataRow<string>(Path.GetFileNameWithoutExtension(path)));

    [Theory]
    [MemberData(nameof(Cases))]
    public void RoundTrips(string caseName)
    {
        if (FindLuauRuntime() is not { } runtime)
        {
            // Skipping is a convenience for a local run without Lune installed. On CI it would quietly
            // retire the only coverage that executes what the generator produces, so it fails instead.
            Assert.False(
                Environment.GetEnvironmentVariable("CI") is { Length: > 0 },
                "No Luau runtime on CI. The round-trip suite is the only coverage that executes the emitted serializers."
            );

            Assert.Skip("No Luau runtime found. Install Lune, or set LUNE_PATH, to run serialization round-trips.");
            return;
        }

        var emitted = File.ReadAllText(Path.Combine(AssemblyFixture.Snapshots, "Luau", $"{caseName}.luau"));
        var assertions = File.ReadAllText(Path.Combine(RuntimeDirectory, $"{caseName}.luau"));
        var scriptPath = Path.Combine(Path.GetTempPath(), $"loom-roundtrip-{caseName}-{Guid.NewGuid():N}.luau");

        try
        {
            File.WriteAllText(scriptPath, Assemble(emitted, assertions));
            var (exitCode, output) = Run(runtime, scriptPath);
            Assert.True(exitCode == 0, $"{caseName} did not round-trip:\n{output}");
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    /// <summary>
    ///     Splices the emitted serializers between the stubs and the assertions. The runtime import is
    ///     dropped in favour of the stubbed table, and type aliases are stripped because they name Roblox
    ///     types the stubs only model structurally.
    /// </summary>
    private static string Assemble(string emitted, string assertions)
    {
        var body = string.Join(
            '\n',
            emitted.Split('\n')
                .Where(line => !line.Contains("require(", StringComparison.Ordinal))
                .Where(line => !line.StartsWith("type ", StringComparison.Ordinal))
                .Where(line => !line.StartsWith("export type ", StringComparison.Ordinal))
                .Where(line => !line.StartsWith("  read ", StringComparison.Ordinal))
                .Where(line => line.TrimEnd() != "}")
        );

        var prelude = File.ReadAllText(Path.Combine(RuntimeDirectory, "prelude.luau"));
        return $"{prelude}\n-- emitted\n{body}\n-- assertions\n{assertions}\n";
    }

    /// <summary>
    ///     Runs the script, retrying against a <c>const</c>-free copy if the runtime rejects the keyword.
    ///     Loom spells immutable bindings <c>const</c>; older Luau builds have no such keyword, and the
    ///     distinction is irrelevant to whether an encoding round-trips.
    /// </summary>
    private static (int ExitCode, string Output) Run(string runtime, string scriptPath)
    {
        var (exitCode, output) = Execute(runtime, scriptPath);
        if (exitCode == 0 || !output.Contains("Incomplete statement", StringComparison.Ordinal))
            return (exitCode, output);

        File.WriteAllText(scriptPath, File.ReadAllText(scriptPath).Replace("const ", "local "));
        return Execute(runtime, scriptPath);
    }

    private static (int ExitCode, string Output) Execute(string runtime, string scriptPath)
    {
        using var process = Process.Start(
            new ProcessStartInfo(runtime, ["run", scriptPath])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        )!;

        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output.Trim());
    }

    private static string? FindLuauRuntime()
    {
        if (Environment.GetEnvironmentVariable("LUNE_PATH") is { Length: > 0 } configured && File.Exists(configured))
            return configured;

        var extensions = OperatingSystem.IsWindows() ? new[] { ".exe", "" } : [""];
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (directory.Length == 0)
                continue;

            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, "lune" + extension);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
