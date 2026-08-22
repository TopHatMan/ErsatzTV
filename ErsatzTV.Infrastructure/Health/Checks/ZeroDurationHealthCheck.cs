using ErsatzTV.Core.Health;
using ErsatzTV.Core.Health.Checks;
using ErsatzTV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErsatzTV.Infrastructure.Health.Checks;

public class ZeroDurationHealthCheck : BaseHealthCheck, IZeroDurationHealthCheck
{
    private readonly IDbContextFactory<TvContext> _dbContextFactory;

    public ZeroDurationHealthCheck(IDbContextFactory<TvContext> dbContextFactory) =>
        _dbContextFactory = dbContextFactory;

    public override string Title => "Zero Duration";

    public async Task<HealthCheckResult> Check(CancellationToken cancellationToken)
    {
        await using TvContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        int movies = await dbContext.Movies
            .CountAsync(m => m.MediaVersions.Any(mv => mv.Duration == TimeSpan.Zero), cancellationToken);
        int episodes = await dbContext.Episodes
            .CountAsync(e => e.MediaVersions.Any(mv => mv.Duration == TimeSpan.Zero), cancellationToken);
        int musicVideos = await dbContext.MusicVideos
            .CountAsync(mv => mv.MediaVersions.Any(v => v.Duration == TimeSpan.Zero), cancellationToken);
        int otherVideos = await dbContext.OtherVideos
            .CountAsync(ov => ov.MediaVersions.Any(mv => mv.Duration == TimeSpan.Zero), cancellationToken);
        int songs = await dbContext.Songs
            .CountAsync(s => s.MediaVersions.Any(mv => mv.Duration == TimeSpan.Zero), cancellationToken);

        int count = movies + episodes + musicVideos + otherVideos + songs;

        if (count != 0)
        {
            return WarningResult(
                $"There are {count} files with zero duration. Open the full list to export, fix, or delete them so they can be redownloaded.",
                $"There are {count} files with zero duration",
                new HealthCheckLink("media/zero-duration"));
        }

        return OkResult();
    }
}
