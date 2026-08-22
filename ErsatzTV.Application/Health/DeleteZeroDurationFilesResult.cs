namespace ErsatzTV.Application.Health;

public record DeleteZeroDurationFilesResult(
    int Requested,
    int DeletedFromDisk,
    int AlreadyMissing,
    int DiskDeleteFailed,
    int FlaggedMissing,
    int RemovedFromLibrary,
    IReadOnlyList<string> Failures);
