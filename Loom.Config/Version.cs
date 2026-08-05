using System.Diagnostics.CodeAnalysis;

namespace Loom.Config;

/// <summary>
///     A semantic version (<c>major.minor.patch[-prerelease][+build]</c>) as written in a package manifest.
///     Ordering follows semver precedence, so build metadata takes part in neither comparison nor equality.
/// </summary>
public sealed class Version : IEquatable<Version>, IComparable<Version>, IComparable
{
    public Version(int major, int minor, int patch, string? prerelease = null, string? buildMetadata = null)
    {
        if (major < 0 || minor < 0 || patch < 0)
            throw new ArgumentOutOfRangeException(nameof(major), "version components cannot be negative.");

        if (prerelease != null && !ValidateIdentifiers(prerelease, "pre-release", rejectLeadingZeroes: true, out var prereleaseError))
            throw new FormatException(prereleaseError);

        if (buildMetadata != null && !ValidateIdentifiers(buildMetadata, "build metadata", rejectLeadingZeroes: false, out var buildError))
            throw new FormatException(buildError);

        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
        BuildMetadata = buildMetadata;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    /// <summary>The dot-separated identifiers after the <c>-</c>, or <see langword="null" /> for a stable release.</summary>
    public string? Prerelease { get; }

    /// <summary>The dot-separated identifiers after the <c>+</c>; ignored when comparing versions.</summary>
    public string? BuildMetadata { get; }

    public bool IsPrerelease => Prerelease != null;

    public static Version Parse(string? text) =>
        TryParse(text, out var version, out var error) ? version : throw new FormatException(error);

    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out Version? version) =>
        TryParse(text, out version, out _);

    public static bool TryParse(
        [NotNullWhen(true)] string? text,
        [NotNullWhen(true)] out Version? version,
        [NotNullWhen(false)] out string? error
    )
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "version cannot be empty.";
            return false;
        }

        var remainder = text.AsSpan();
        string? buildMetadata = null;
        var buildStart = remainder.IndexOf('+');
        if (buildStart >= 0)
        {
            buildMetadata = remainder[(buildStart + 1)..].ToString();
            remainder = remainder[..buildStart];
        }

        string? prerelease = null;
        var prereleaseStart = remainder.IndexOf('-');
        if (prereleaseStart >= 0)
        {
            prerelease = remainder[(prereleaseStart + 1)..].ToString();
            remainder = remainder[..prereleaseStart];
        }

        var components = remainder.ToString().Split('.');
        if (components.Length != 3)
        {
            error = $"version '{text}' must have exactly three components, written 'major.minor.patch'.";
            return false;
        }

        var names = new[] { "major", "minor", "patch" };
        var numbers = new int[3];
        for (var index = 0; index < 3; index++)
        {
            if (!TryParseNumericIdentifier(components[index], out numbers[index]))
            {
                error = $"version '{text}' has an invalid {names[index]} component '{components[index]}'.";
                return false;
            }
        }

        if (prerelease != null && !ValidateIdentifiers(prerelease, "pre-release", rejectLeadingZeroes: true, out error))
            return false;

        if (buildMetadata != null && !ValidateIdentifiers(buildMetadata, "build metadata", rejectLeadingZeroes: false, out error))
            return false;

        version = new Version(numbers[0], numbers[1], numbers[2], prerelease, buildMetadata);
        error = null;
        return true;
    }

    public int CompareTo(Version? other)
    {
        if (other == null)
            return 1;

        var major = Major.CompareTo(other.Major);
        if (major != 0)
            return major;

        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0)
            return minor;

        var patch = Patch.CompareTo(other.Patch);
        return patch != 0 ? patch : ComparePrerelease(Prerelease, other.Prerelease);
    }

    public int CompareTo(object? obj) =>
        obj switch
        {
            null => 1,
            Version other => CompareTo(other),
            _ => throw new ArgumentException($"cannot compare a version to {obj.GetType().Name}.", nameof(obj))
        };

    public bool Equals(Version? other) =>
        other != null && Major == other.Major && Minor == other.Minor && Patch == other.Patch && Prerelease == other.Prerelease;

    public override bool Equals(object? obj) => obj is Version other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Prerelease);

    public static bool operator ==(Version? left, Version? right) => left?.Equals(right) ?? right is null;
    public static bool operator !=(Version? left, Version? right) => !(left == right);
    public static bool operator <(Version? left, Version? right) => Compare(left, right) < 0;
    public static bool operator >(Version? left, Version? right) => Compare(left, right) > 0;
    public static bool operator <=(Version? left, Version? right) => Compare(left, right) <= 0;
    public static bool operator >=(Version? left, Version? right) => Compare(left, right) >= 0;

    public override string ToString()
    {
        var version = $"{Major}.{Minor}.{Patch}";
        if (Prerelease != null)
            version += $"-{Prerelease}";

        return BuildMetadata == null ? version : version + $"+{BuildMetadata}";
    }

    private static int Compare(Version? left, Version? right) => left == null ? right == null ? 0 : -1 : left.CompareTo(right);

    /// <summary>A version with pre-release identifiers has lower precedence than the same version without them.</summary>
    private static int ComparePrerelease(string? left, string? right)
    {
        if (left == null || right == null)
            return left == right ? 0 : left == null ? 1 : -1;

        var leftIdentifiers = left.Split('.');
        var rightIdentifiers = right.Split('.');
        for (var index = 0; index < Math.Min(leftIdentifiers.Length, rightIdentifiers.Length); index++)
        {
            var comparison = CompareIdentifier(leftIdentifiers[index], rightIdentifiers[index]);
            if (comparison != 0)
                return comparison;
        }

        return leftIdentifiers.Length.CompareTo(rightIdentifiers.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftIsNumeric = TryParseNumericIdentifier(left, out var leftNumber);
        var rightIsNumeric = TryParseNumericIdentifier(right, out var rightNumber);
        if (leftIsNumeric && rightIsNumeric)
            return leftNumber.CompareTo(rightNumber);

        // numeric identifiers always have lower precedence than alphanumeric ones.
        if (leftIsNumeric != rightIsNumeric)
            return leftIsNumeric ? -1 : 1;

        return string.CompareOrdinal(left, right);
    }

    private static bool TryParseNumericIdentifier(string text, out int value)
    {
        value = 0;
        if (text.Length == 0 || !text.All(char.IsAsciiDigit))
            return false;

        // leading zeroes are not allowed in numeric identifiers.
        return (text.Length == 1 || text[0] != '0') && int.TryParse(text, out value);
    }

    private static bool ValidateIdentifiers(string text, string kind, bool rejectLeadingZeroes, [NotNullWhen(false)] out string? error)
    {
        if (text.Length == 0)
        {
            error = $"{kind} identifiers cannot be empty.";
            return false;
        }

        foreach (var identifier in text.Split('.'))
        {
            if (identifier.Length == 0)
            {
                error = $"{kind} identifiers cannot be empty.";
                return false;
            }

            if (!identifier.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'))
            {
                error = $"{kind} identifier '{identifier}' may only contain letters, digits and '-'.";
                return false;
            }

            var isNumeric = identifier.All(char.IsAsciiDigit);
            if (rejectLeadingZeroes && isNumeric && identifier.Length > 1 && identifier[0] == '0')
            {
                error = $"{kind} identifier '{identifier}' cannot have leading zeroes.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
