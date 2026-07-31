using ErsatzTV.Application.Filler;
using ErsatzTV.Core.Domain;

namespace ErsatzTV.ViewModels;

public sealed class ProgramScheduleItemBulkEditViewModel
{
    public bool ApplyPlaybackOrder { get; set; }
    public PlaybackOrder PlaybackOrder { get; set; } = PlaybackOrder.Chronological;

    public bool ApplyGuideMode { get; set; }
    public GuideMode GuideMode { get; set; } = GuideMode.Normal;

    public bool ApplyPreRollFiller { get; set; }
    public FillerPresetViewModel PreRollFiller { get; set; }

    public bool ApplyMidRollFiller { get; set; }
    public FillerPresetViewModel MidRollFiller { get; set; }

    public bool ApplyPostRollFiller { get; set; }
    public FillerPresetViewModel PostRollFiller { get; set; }

    public bool ApplyTailFiller { get; set; }
    public FillerPresetViewModel TailFiller { get; set; }

    public bool ApplyFallbackFiller { get; set; }
    public FillerPresetViewModel FallbackFiller { get; set; }

    public void ApplyTo(IEnumerable<ProgramScheduleItemEditViewModel> items)
    {
        foreach (ProgramScheduleItemEditViewModel item in items)
        {
            if (ApplyPlaybackOrder)
            {
                item.PlaybackOrder = PlaybackOrder;
            }

            if (ApplyGuideMode)
            {
                item.GuideMode = GuideMode;
            }

            if (ApplyPreRollFiller)
            {
                item.PreRollFiller = PreRollFiller;
            }

            if (ApplyMidRollFiller)
            {
                item.MidRollFiller = MidRollFiller;
            }

            if (ApplyPostRollFiller)
            {
                item.PostRollFiller = PostRollFiller;
            }

            if (ApplyTailFiller)
            {
                item.TailFiller = TailFiller;
            }

            if (ApplyFallbackFiller)
            {
                item.FallbackFiller = FallbackFiller;
            }
        }
    }

    public static IReadOnlyList<PlaybackOrder> CommonPlaybackOrders(
        IEnumerable<ProgramScheduleItemEditViewModel> items)
    {
        List<ProgramScheduleItemEditViewModel> selected = items.ToList();
        if (selected.Count == 0)
        {
            return [];
        }

        List<PlaybackOrder> result = PlaybackOrdersFor(selected[0].CollectionType).ToList();
        foreach (ProgramScheduleItemEditViewModel item in selected.Skip(1))
        {
            IReadOnlyList<PlaybackOrder> available = PlaybackOrdersFor(item.CollectionType);
            result.RemoveAll(order => !available.Contains(order));
        }

        return result;
    }

    public static bool CanEditTailFiller(IEnumerable<ProgramScheduleItemEditViewModel> items)
    {
        List<ProgramScheduleItemEditViewModel> selected = items.ToList();
        return selected.Count > 0 && selected.All(
            item => item.PlayoutMode is PlayoutMode.Duration && item.TailMode is TailMode.Filler);
    }

    private static IReadOnlyList<PlaybackOrder> PlaybackOrdersFor(CollectionType collectionType) => collectionType switch
    {
        CollectionType.MultiCollection =>
        [
            PlaybackOrder.Shuffle,
            PlaybackOrder.ShuffleInOrder
        ],
        CollectionType.Collection or CollectionType.SmartCollection or CollectionType.SearchQuery =>
        [
            PlaybackOrder.Chronological,
            PlaybackOrder.RotateStart,
            PlaybackOrder.Random,
            PlaybackOrder.Shuffle,
            PlaybackOrder.ShuffleInOrder,
            PlaybackOrder.Marathon
        ],
        CollectionType.TelevisionShow =>
        [
            PlaybackOrder.Chronological,
            PlaybackOrder.SeasonEpisode,
            PlaybackOrder.RotateStart,
            PlaybackOrder.Random,
            PlaybackOrder.Shuffle,
            PlaybackOrder.MultiEpisodeShuffle
        ],
        CollectionType.Playlist or CollectionType.RerunFirstRun or CollectionType.RerunRerun => [],
        _ =>
        [
            PlaybackOrder.Chronological,
            PlaybackOrder.RotateStart,
            PlaybackOrder.Random,
            PlaybackOrder.Shuffle
        ]
    };
}
