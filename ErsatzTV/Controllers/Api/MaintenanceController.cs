using System.Text;
using System.Threading.Channels;
using ErsatzTV.Application;
using ErsatzTV.Application.Health;
using ErsatzTV.Application.Maintenance;
using ErsatzTV.Core;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ErsatzTV.Controllers.Api;

[ApiController]
[EndpointGroupName("general")]
public class MaintenanceController(IMediator mediator, ChannelWriter<IBackgroundServiceRequest> workerChannel)
{
    [HttpGet("/api/maintenance/gc")]
    [Tags("Maintenance")]
    [EndpointSummary("Garbage collect")]
    public async Task<IActionResult> GarbageCollection([FromQuery] bool force = false)
    {
        await mediator.Send(new ReleaseMemory(force));
        return new OkResult();
    }

    [HttpPost("/api/maintenance/empty_trash")]
    [Tags("Maintenance")]
    [EndpointSummary("Empty trash")]
    public async Task<IActionResult> EmptyTrash()
    {
        Either<BaseError, Unit> result = await mediator.Send(new EmptyTrash());
        foreach (BaseError error in result.LeftToSeq())
        {
            return new ContentResult
            {
                StatusCode = StatusCodes.Status500InternalServerError,
                Content = error.ToString(),
                ContentType = "text/plain"
            };
        }

        return new OkResult();
    }

    [HttpPost("/api/maintenance/clean_artwork")]
    [Tags("Maintenance")]
    [EndpointSummary("Clean artwork cache")]
    public async Task<IActionResult> CleanArtwork(CancellationToken cancellationToken, [FromQuery] int limit = 100_000)
    {
        await workerChannel.WriteAsync(new DeleteOrphanedArtwork(limit), cancellationToken);
        return new OkResult();
    }

    [HttpGet("/api/maintenance/zero_duration")]
    [Tags("Maintenance")]
    [EndpointSummary("List files with zero duration")]
    public async Task<ActionResult<ZeroDurationFilesViewModel>> GetZeroDurationFiles(
        CancellationToken cancellationToken) =>
        await mediator.Send(new GetZeroDurationFiles(), cancellationToken);

    [HttpGet("/api/maintenance/zero_duration.csv")]
    [Tags("Maintenance")]
    [EndpointSummary("Export zero-duration files as CSV")]
    public async Task<IActionResult> ExportZeroDurationCsv(CancellationToken cancellationToken)
    {
        ZeroDurationFilesViewModel result = await mediator.Send(new GetZeroDurationFiles(), cancellationToken);
        byte[] bytes = Encoding.UTF8.GetBytes(ZeroDurationFileExport.ToCsv(result.Files));
        return new FileContentResult(bytes, "text/csv") { FileDownloadName = "zero-duration-files.csv" };
    }

    [HttpGet("/api/maintenance/zero_duration.txt")]
    [Tags("Maintenance")]
    [EndpointSummary("Export zero-duration file paths as a plain list")]
    public async Task<IActionResult> ExportZeroDurationPaths(CancellationToken cancellationToken)
    {
        ZeroDurationFilesViewModel result = await mediator.Send(new GetZeroDurationFiles(), cancellationToken);
        byte[] bytes = Encoding.UTF8.GetBytes(ZeroDurationFileExport.ToPathList(result.Files));
        return new FileContentResult(bytes, "text/plain") { FileDownloadName = "zero-duration-files.txt" };
    }

    [HttpPost("/api/maintenance/zero_duration/delete")]
    [Tags("Maintenance")]
    [EndpointSummary("Delete zero-duration files from disk and/or the library")]
    public async Task<IActionResult> DeleteZeroDurationFiles(
        [FromBody] DeleteZeroDurationFilesApiRequest body,
        [FromQuery] string confirm,
        CancellationToken cancellationToken)
    {
        if (body is null)
        {
            return new ContentResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Content = "Request body is required.",
                ContentType = "text/plain"
            };
        }

        if (body.All && !string.Equals(confirm, "NUKE", StringComparison.Ordinal))
        {
            return new ContentResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Content = "Deleting every zero-duration file requires confirm=NUKE.",
                ContentType = "text/plain"
            };
        }

        var command = new DeleteZeroDurationFiles(
            body.MediaItemIds ?? [],
            body.All,
            body.DeleteFromDisk,
            body.RemoveFromLibrary);

        Either<BaseError, DeleteZeroDurationFilesResult> result =
            await mediator.Send(command, cancellationToken);

        foreach (BaseError error in result.LeftToSeq())
        {
            return new ContentResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Content = error.ToString(),
                ContentType = "text/plain"
            };
        }

        return new OkObjectResult(result.RightToSeq().Head());
    }
}
