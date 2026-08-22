using System.Globalization;
using System.Text;

namespace ErsatzTV.Application.Health;

public static class ZeroDurationFileExport
{
    public static string ToCsv(IEnumerable<ZeroDurationFileViewModel> files)
    {
        var builder = new StringBuilder();
        builder.AppendLine("MediaItemId,Kind,Title,Path,Location,Library,State");

        foreach (ZeroDurationFileViewModel file in files)
        {
            builder.Append(file.MediaItemId.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(Escape(file.Kind));
            builder.Append(',');
            builder.Append(Escape(file.Title));
            builder.Append(',');
            builder.Append(Escape(file.Path));
            builder.Append(',');
            builder.Append(Escape(file.Location));
            builder.Append(',');
            builder.Append(Escape(file.LibraryName));
            builder.Append(',');
            builder.AppendLine(Escape(file.State));
        }

        return builder.ToString();
    }

    public static string ToPathList(IEnumerable<ZeroDurationFileViewModel> files)
    {
        var builder = new StringBuilder();
        foreach (ZeroDurationFileViewModel file in files)
        {
            if (!string.IsNullOrWhiteSpace(file.Path))
            {
                builder.AppendLine(file.Path);
            }
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}
