using System.IO;

namespace TagExplorer.Services;

public static class PathNormalizer
{
    public static string NormalizeAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be null or whitespace.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path).Trim();

        fullPath = fullPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        if (fullPath.Length > 1)
        {
            fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar);
        }

        return fullPath;
    }

    public static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var normalizedLeft = NormalizeAbsolutePath(left);
        var normalizedRight = NormalizeAbsolutePath(right);

        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }
}
