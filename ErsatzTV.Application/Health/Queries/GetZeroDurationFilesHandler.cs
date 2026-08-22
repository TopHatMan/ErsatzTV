using ErsatzTV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErsatzTV.Application.Health;

public class GetZeroDurationFilesHandler(IDbContextFactory<TvContext> dbContextFactory)
    : IRequestHandler<GetZeroDurationFiles, ZeroDurationFilesViewModel>
{
    public async Task<ZeroDurationFilesViewModel> Handle(
        GetZeroDurationFiles request,
        CancellationToken cancellationToken)
    {
        await using TvContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<ZeroDurationFileViewModel> files = await QueryFiles(dbContext, cancellationToken);

        List<ZeroDurationLocationSummary> locations = files
            .GroupBy(f => f.Location, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Map(g => new ZeroDurationLocationSummary(g.Key, g.Count()))
            .ToList();

        return new ZeroDurationFilesViewModel(files.Count, files, locations);
    }

    public static async Task<List<ZeroDurationFileViewModel>> QueryFiles(
        TvContext dbContext,
        CancellationToken cancellationToken)
    {
        var files = new List<ZeroDurationFileViewModel>();
        files.AddRange(await QueryMovies(dbContext, cancellationToken));
        files.AddRange(await QueryEpisodes(dbContext, cancellationToken));
        files.AddRange(await QueryMusicVideos(dbContext, cancellationToken));
        files.AddRange(await QueryOtherVideos(dbContext, cancellationToken));
        files.AddRange(await QuerySongs(dbContext, cancellationToken));
        return files.OrderBy(f => f.Location, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<ZeroDurationFileViewModel>> QueryMovies(
        TvContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Movies
            .AsNoTracking()
            .Where(m => m.MediaVersions.Any(mv => mv.Duration == TimeSpan.Zero))
            .Select(m => new
            {
                m.Id,
                Title = m.MovieMetadata.Select(mm => mm.Title).FirstOrDefault(),
                Path = m.MediaVersions.SelectMany(mv => mv.MediaFiles).Select(mf => mf.Path).FirstOrDefault(),
                LibraryName = m.LibraryPath.Library.Name,
                m.State
            })
            .ToListAsync(cancellationToken);

        return rows.Map(r => new ZeroDurationFileViewModel(
            r.Id,
            "Movie",
            string.IsNullOrWhiteSpace(r.Title) ? "[unknown movie]" : r.Title,
            r.Path ?? string.Empty,
            r.LibraryName ?? string.Empty,
            r.State.ToString())).ToList();
    }

    private static async Task<List<ZeroDurationFileViewModel>> QueryEpisodes(
        TvContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Episodes
            .AsNoTracking()
            .Where(e => e.MediaVersions.Any(mv => mv.Duration == TimeSpan.Zero))
            .Select(e => new
            {
                e.Id,
                ShowTitle = e.Season.Show.ShowMetadata.Select(sm => sm.Title).FirstOrDefault(),
                e.Season.SeasonNumber,
                EpisodeNumber = e.EpisodeMetadata.Select(em => em.EpisodeNumber).FirstOrDefault(),
                EpisodeTitle = e.EpisodeMetadata.Select(em => em.Title).FirstOrDefault(),
                Path = e.MediaVersions.SelectMany(mv => mv.MediaFiles).Select(mf => mf.Path).FirstOrDefault(),
                LibraryName = e.LibraryPath.Library.Name,
                e.State
            })
            .ToListAsync(cancellationToken);

        return rows.Map(r =>
        {
            string show = string.IsNullOrWhiteSpace(r.ShowTitle) ? "[unknown show]" : r.ShowTitle;
            string episodeTitle = string.IsNullOrWhiteSpace(r.EpisodeTitle) ? "[unknown episode]" : r.EpisodeTitle;
            string title =
                $"{show} - s{r.SeasonNumber:00}e{r.EpisodeNumber:00} - {episodeTitle}";
            return new ZeroDurationFileViewModel(
                r.Id,
                "Episode",
                title,
                r.Path ?? string.Empty,
                r.LibraryName ?? string.Empty,
                r.State.ToString());
        }).ToList();
    }

    private static async Task<List<ZeroDurationFileViewModel>> QueryMusicVideos(
        TvContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.MusicVideos
            .AsNoTracking()
            .Where(mv => mv.MediaVersions.Any(v => v.Duration == TimeSpan.Zero))
            .Select(mv => new
            {
                mv.Id,
                Artist = mv.Artist.ArtistMetadata.Select(am => am.Title).FirstOrDefault(),
                Title = mv.MusicVideoMetadata.Select(m => m.Title).FirstOrDefault(),
                Path = mv.MediaVersions.SelectMany(v => v.MediaFiles).Select(mf => mf.Path).FirstOrDefault(),
                LibraryName = mv.LibraryPath.Library.Name,
                mv.State
            })
            .ToListAsync(cancellationToken);

        return rows.Map(r =>
        {
            string title = string.IsNullOrWhiteSpace(r.Artist)
                ? r.Title
                : $"{r.Artist} - {r.Title}";
            return new ZeroDurationFileViewModel(
                r.Id,
                "Music Video",
                string.IsNullOrWhiteSpace(title) ? "[unknown music video]" : title,
                r.Path ?? string.Empty,
                r.LibraryName ?? string.Empty,
                r.State.ToString());
        }).ToList();
    }

    private static async Task<List<ZeroDurationFileViewModel>> QueryOtherVideos(
        TvContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.OtherVideos
            .AsNoTracking()
            .Where(ov => ov.MediaVersions.Any(mv => mv.Duration == TimeSpan.Zero))
            .Select(ov => new
            {
                ov.Id,
                Title = ov.OtherVideoMetadata.Select(m => m.Title).FirstOrDefault(),
                Path = ov.MediaVersions.SelectMany(mv => mv.MediaFiles).Select(mf => mf.Path).FirstOrDefault(),
                LibraryName = ov.LibraryPath.Library.Name,
                ov.State
            })
            .ToListAsync(cancellationToken);

        return rows.Map(r => new ZeroDurationFileViewModel(
            r.Id,
            "Other Video",
            string.IsNullOrWhiteSpace(r.Title) ? "[unknown video]" : r.Title,
            r.Path ?? string.Empty,
            r.LibraryName ?? string.Empty,
            r.State.ToString())).ToList();
    }

    private static async Task<List<ZeroDurationFileViewModel>> QuerySongs(
        TvContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Songs
            .AsNoTracking()
            .Where(s => s.MediaVersions.Any(mv => mv.Duration == TimeSpan.Zero))
            .Select(s => new
            {
                s.Id,
                Title = s.SongMetadata.Select(m => m.Title).FirstOrDefault(),
                Path = s.MediaVersions.SelectMany(mv => mv.MediaFiles).Select(mf => mf.Path).FirstOrDefault(),
                LibraryName = s.LibraryPath.Library.Name,
                s.State
            })
            .ToListAsync(cancellationToken);

        return rows.Map(r => new ZeroDurationFileViewModel(
            r.Id,
            "Song",
            string.IsNullOrWhiteSpace(r.Title) ? "[unknown song]" : r.Title,
            r.Path ?? string.Empty,
            r.LibraryName ?? string.Empty,
            r.State.ToString())).ToList();
    }
}
