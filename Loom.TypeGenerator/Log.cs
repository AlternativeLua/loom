using System.Diagnostics.CodeAnalysis;

namespace Loom.TypeGenerator;

internal static class Log
{
    public static void Info(string message) => Console.WriteLine($"info: {message}");

    [DoesNotReturn]
    public static void Fatal(string message, int code = 1)
    {
        Console.Error.WriteLine($"fatal: {message}");
        Environment.Exit(code);
    }
}