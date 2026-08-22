using ErsatzTV.Core;

namespace ErsatzTV.Application.Health;

public record DeleteZeroDurationFiles(
    IReadOnlyList<int> MediaItemIds,
    bool All,
    bool DeleteFromDisk,
    bool RemoveFromLibrary) : IRequest<Either<BaseError, DeleteZeroDurationFilesResult>>;

public class DeleteZeroDurationFilesApiRequest
{
    public List<int> MediaItemIds { get; set; } = [];
    public bool All { get; set; }
    public bool DeleteFromDisk { get; set; } = true;
    public bool RemoveFromLibrary { get; set; }
}
