using System.IO.Abstractions;
using Dapper;
using ErsatzTV.Core;
using ErsatzTV.Core.Health;
using ErsatzTV.Core.Interfaces.Metadata;
using ErsatzTV.Core.Interfaces.Repositories;
using ErsatzTV.Core.Interfaces.Search;
using ErsatzTV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErsatzTV.Application.Health;

public class DeleteZeroDurationFilesHandler(
    IDbContextFactory<TvContext> dbContextFactory,
    IFileSystem fileSystem,
    IMediaItemRepository mediaItemRepository,
    ISearchIndex searchIndex,
    ISearchRepository searchRepository,
    IFallbackMetadataProvider fallbackMetadataProvider,
    ILanguageCodeService languageCodeService,
    ILogger<DeleteZeroDurationFilesHandler> logger)
    : IRequestHandler<DeleteZeroDurationFiles, Either<BaseError, DeleteZeroDurationFilesResult>>
{
    public async Task<Either<BaseError, DeleteZeroDurationFilesResult>> Handle(
        DeleteZeroDurationFiles request,
        CancellationToken cancellationToken)
    {
        if (!request.DeleteFromDisk && !request.RemoveFromLibrary)
        {
            return BaseError.New("Choose at least one action: delete from disk or remove from library.");
        }

        await using TvContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<ZeroDurationFileViewModel> allFiles = await GetZeroDurationFilesHandler.QueryFiles(
            dbContext,
            cancellationToken);

        var requestedIds = new System.Collections.Generic.HashSet<int>(request.MediaItemIds ?? []);
        List<ZeroDurationFileViewModel> selected = request.All
            ? allFiles
            : allFiles.Filter(f => requestedIds.Contains(f.MediaItemId)).ToList();

        if (selected.Count == 0)
        {
            return BaseError.New("No zero-duration files matched the request.");
        }

        int deletedFromDisk = 0;
        int alreadyMissing = 0;
        int diskDeleteFailed = 0;
        var failures = new List<string>();
        var deletedOrMissingIds = new List<int>();

        if (request.DeleteFromDisk)
        {
            foreach (IGrouping<string, ZeroDurationFileViewModel> group in selected.GroupBy(f => f.Location))
            {
                if (MediaPathHelper.IsWindowsDriveLocation(group.Key) && !IsDriveReady(group.Key))
                {
                    foreach (ZeroDurationFileViewModel file in group)
                    {
                        diskDeleteFailed++;
                        failures.Add($"{file.Path}: drive {group.Key} is not ready");
                    }

                    continue;
                }

                foreach (ZeroDurationFileViewModel file in group)
                {
                    if (string.IsNullOrWhiteSpace(file.Path))
                    {
                        alreadyMissing++;
                        deletedOrMissingIds.Add(file.MediaItemId);
                        continue;
                    }

                    try
                    {
                        if (!fileSystem.File.Exists(file.Path))
                        {
                            alreadyMissing++;
                            deletedOrMissingIds.Add(file.MediaItemId);
                            continue;
                        }

                        fileSystem.File.Delete(file.Path);
                        deletedFromDisk++;
                        deletedOrMissingIds.Add(file.MediaItemId);
                    }
                    catch (Exception ex)
                    {
                        diskDeleteFailed++;
                        failures.Add($"{file.Path}: {ex.Message}");
                    }
                }
            }
        }

        int flaggedMissing = 0;
        int removedFromLibrary = 0;

        if (request.RemoveFromLibrary)
        {
            var ids = selected.Map(f => f.MediaItemId).Distinct().ToList();
            Either<BaseError, Unit> deleteResult = await mediaItemRepository.DeleteItems(ids);
            foreach (BaseError error in deleteResult.LeftToSeq())
            {
                return error;
            }

            await searchIndex.RemoveItems(ids);
            searchIndex.Commit();
            removedFromLibrary = ids.Count;
        }
        else if (deletedOrMissingIds.Count > 0)
        {
            var ids = deletedOrMissingIds.Distinct().ToList();
            foreach (int[] chunk in ids.Chunk(400))
            {
                await dbContext.Connection.ExecuteAsync(
                    new CommandDefinition(
                        "UPDATE MediaItem SET State = 1 WHERE Id IN @Ids",
                        new { Ids = chunk },
                        cancellationToken: cancellationToken));
            }

            await searchIndex.RebuildItems(
                searchRepository,
                fallbackMetadataProvider,
                languageCodeService,
                ids,
                cancellationToken);
            searchIndex.Commit();
            flaggedMissing = ids.Count;
        }

        logger.LogInformation(
            "Zero-duration cleanup: requested {Requested}, deleted {Deleted}, missing {Missing}, failed {Failed}, removed {Removed}",
            selected.Count,
            deletedFromDisk,
            alreadyMissing,
            diskDeleteFailed,
            removedFromLibrary);

        return new DeleteZeroDurationFilesResult(
            selected.Count,
            deletedFromDisk,
            alreadyMissing,
            diskDeleteFailed,
            flaggedMissing,
            removedFromLibrary,
            failures);
    }

    private bool IsDriveReady(string location)
    {
        try
        {
            string name = location.Length >= 2 ? location[..2] : location;
            IDriveInfo drive = fileSystem.DriveInfo.New(name);
            return drive.IsReady;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
