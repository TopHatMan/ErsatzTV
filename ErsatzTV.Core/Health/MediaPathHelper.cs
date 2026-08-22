namespace ErsatzTV.Core.Health;

public static class MediaPathHelper
{
    public static string GetLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "(unknown)";
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            string trimmed = path.TrimStart('\\', '/');
            string[] parts = trimmed.Split(['\\', '/'], 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $@"\\{parts[0]}\{parts[1]}";
            }

            return @"\\";
        }

        if (path.Length >= 2 && path[1] == ':')
        {
            return $"{char.ToUpperInvariant(path[0])}:\\";
        }

        string[] unixParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return unixParts.Length switch
        {
            0 => "/",
            1 => "/" + unixParts[0],
            _ => "/" + unixParts[0] + "/" + unixParts[1]
        };
    }

    public static bool IsWindowsDriveLocation(string location) =>
        !string.IsNullOrWhiteSpace(location) && location.Length >= 2 && location[1] == ':';
}
