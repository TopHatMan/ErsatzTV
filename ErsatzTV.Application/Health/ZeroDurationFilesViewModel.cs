namespace ErsatzTV.Application.Health;

public record ZeroDurationFilesViewModel(
    int Count,
    List<ZeroDurationFileViewModel> Files,
    List<ZeroDurationLocationSummary> Locations);
