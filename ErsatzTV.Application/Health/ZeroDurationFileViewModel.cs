using ErsatzTV.Core.Health;

namespace ErsatzTV.Application.Health;

public record ZeroDurationFileViewModel(
    int MediaItemId,
    string Kind,
    string Title,
    string Path,
    string LibraryName,
    string State)
{
    public string Location => MediaPathHelper.GetLocation(Path);
}
